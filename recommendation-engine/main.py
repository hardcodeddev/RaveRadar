from __future__ import annotations
import asyncio
import os
from datetime import datetime, timezone

import numpy as np
from dotenv import load_dotenv
from fastapi import FastAPI

load_dotenv()

from feature_extractor import (
    fetch_deezer_track,
    fetch_lastfm_tags,
    build_feature_vector,
    VIBE_TAGS,
)
from models import (
    AudioFeatureDto,
    AudioFeatureRequest,
    RecommendRequest,
    RecommendResponse,
    ScoredArtist,
)
from recommender import build_user_profile, score_candidates

app = FastAPI(title="RaveRadar ML Engine")


@app.get("/health")
def health():
    return {"status": "ok"}


async def _fetch_features(
    artist: str,
    song: str,
    genres: list[str],
    vibes: list[str],
) -> tuple[np.ndarray, float, dict | None, list[str]]:
    """Fetch Deezer + Last.fm in parallel and return (vector, bpm, deezer_data, tags)."""
    deezer, tags = await asyncio.gather(
        fetch_deezer_track(artist, song),
        fetch_lastfm_tags(artist, song),
    )
    vec = build_feature_vector(deezer, tags, genres, vibes)
    bpm = float((deezer or {}).get("bpm", 0) or 0)
    return vec, bpm, deezer, tags


@app.post("/recommend", response_model=RecommendResponse)
async def recommend(req: RecommendRequest):
    # Cap candidates to top-20 by popularity (~40 external requests total)
    top_candidates = sorted(req.candidate_artists, key=lambda a: a.popularity, reverse=True)[:20]

    # Fan-out: fetch all features in parallel
    track_tasks = [
        _fetch_features(t.artist_name, t.song_name, t.genres, t.vibes)
        for t in req.saved_tracks
    ]
    candidate_tasks = [
        _fetch_features(
            a.name,
            a.top_tracks[0] if a.top_tracks else a.name,
            a.genres,
            a.vibes,
        )
        for a in top_candidates
    ]

    all_results = await asyncio.gather(*track_tasks, *candidate_tasks)
    track_results = all_results[: len(track_tasks)]
    candidate_results = all_results[len(track_tasks) :]

    # Parse added_at timestamps
    added_at_list: list[datetime] = []
    for t in req.saved_tracks:
        try:
            dt = datetime.fromisoformat(t.added_at.replace("Z", "+00:00")) if t.added_at else datetime.now(timezone.utc)
        except Exception:
            dt = datetime.now(timezone.utc)
        added_at_list.append(dt)

    track_vectors = [r[0] for r in track_results]

    # Build user profile
    user_profile: np.ndarray | None = None
    if len(track_vectors) >= 3:
        user_profile = build_user_profile(track_vectors, added_at_list)
    elif req.favorite_artist_names:
        fav_lower = {n.lower() for n in req.favorite_artist_names}
        fav_vecs = [
            candidate_results[i][0]
            for i, a in enumerate(top_candidates)
            if a.name.lower() in fav_lower
        ]
        if fav_vecs:
            matrix = np.stack(fav_vecs)
            profile = matrix.mean(axis=0)
            norm = np.linalg.norm(profile)
            user_profile = profile / norm if norm > 0 else profile

    if user_profile is None:
        return RecommendResponse(artists=[], source="ml")

    # Build candidate payload for scorer
    candidate_data = [
        {
            "id": top_candidates[i].id,
            "vector": candidate_results[i][0],
            "bpm": candidate_results[i][1],
            "bpm_std": 0.0,
        }
        for i in range(len(top_candidates))
    ]

    scored = score_candidates(user_profile, candidate_data)
    artists = [
        ScoredArtist(id=s["id"], score=round(s["score"], 4), reason=s["reason"])
        for s in scored
    ]
    return RecommendResponse(artists=artists, source="ml")


@app.post("/track-features", response_model=AudioFeatureDto)
async def track_features(req: AudioFeatureRequest):
    deezer, tags = await asyncio.gather(
        fetch_deezer_track(req.artist_name, req.song_name),
        fetch_lastfm_tags(req.artist_name, req.song_name),
    )

    axes = {"energy": 0.0, "danceability": 0.0, "valence": 0.0, "bpm_tier": 0.0, "darkness": 0.0}
    for tag in tags:
        contrib = VIBE_TAGS.get(tag)
        if contrib:
            for axis, weight in contrib.items():
                axes[axis] += weight

    bpm_val: float | None = None
    if deezer:
        bpm = float(deezer.get("bpm", 0) or 0)
        gain = float(deezer.get("gain", 0) or 0)
        if bpm > 0:
            bpm_val = bpm
            axes["bpm_tier"] += float(np.clip((bpm - 80) / 120, 0, 1))
        if gain != 0:
            axes["energy"] += float(np.clip((-gain - 3) / 12, 0, 1))

    def _opt(v: float) -> float | None:
        return float(np.clip(v, -1, 1)) if v != 0.0 else None

    return AudioFeatureDto(
        bpm_value=bpm_val,
        energy_score=_opt(axes["energy"]),
        danceability_score=_opt(axes["danceability"]),
        valence_score=_opt(axes["valence"]),
        darkness_score=_opt(axes["darkness"]),
    )
