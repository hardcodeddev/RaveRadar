import os
import asyncio
import numpy as np
import httpx
from cache import cache, TTL_DEEZER, TTL_LASTFM_TAGS, TTL_LASTFM_SIMILAR
from vibe_tags import VIBE_TAGS

LASTFM_API_KEY = os.getenv("LASTFM_API_KEY", "")

# 15 canonical EDM genres (must match GENRE_LIST in recommender.py)
GENRE_LIST = [
    "house", "tech house", "bass house", "techno", "hard techno",
    "dubstep", "drum & bass", "trance", "trap", "future bass",
    "hardstyle", "ambient", "progressive", "psytrance", "electro",
]

# 8 vibes matching SpotifyService.DeriveVibes()
VIBE_LIST = ["Dark", "Groovy", "Bass Heavy", "Euphoric", "Energetic", "Chill", "Trippy", "Festival"]


async def fetch_deezer_track(artist: str, title: str) -> dict | None:
    key = f"deezer:{artist.lower()}:{title.lower()}"
    cached = cache.get(key)
    if cached is not None:
        return cached

    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            r = await client.get(
                "https://api.deezer.com/search",
                params={"q": f"{artist} {title}", "limit": 1},
            )
            data = r.json()
            if data.get("data"):
                track = data["data"][0]
                result = {
                    "bpm": track.get("bpm", 0) or 0,
                    "gain": track.get("gain", 0) or 0,
                    "duration": track.get("duration", 0),
                    "rank": track.get("rank", 0),
                }
                cache.set(key, result, TTL_DEEZER)
                return result
    except Exception:
        pass
    return None


async def fetch_lastfm_tags(artist: str, title: str) -> list[str]:
    if not LASTFM_API_KEY:
        return []
    key = f"lastfm:tags:{artist.lower()}:{title.lower()}"
    cached = cache.get(key)
    if cached is not None:
        return cached

    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            r = await client.get(
                "http://ws.audioscrobbler.com/2.0/",
                params={
                    "method": "track.getTopTags",
                    "artist": artist,
                    "track": title,
                    "api_key": LASTFM_API_KEY,
                    "format": "json",
                    "limit": 10,
                },
            )
            data = r.json()
            tags = [
                t["name"].lower()
                for t in data.get("toptags", {}).get("tag", [])
            ][:10]
            cache.set(key, tags, TTL_LASTFM_TAGS)
            return tags
    except Exception:
        return []


async def fetch_lastfm_similar_artists(artist: str) -> list[str]:
    if not LASTFM_API_KEY:
        return []
    key = f"lastfm:similar:{artist.lower()}"
    cached = cache.get(key)
    if cached is not None:
        return cached

    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            r = await client.get(
                "http://ws.audioscrobbler.com/2.0/",
                params={
                    "method": "artist.getSimilar",
                    "artist": artist,
                    "api_key": LASTFM_API_KEY,
                    "format": "json",
                    "limit": 10,
                },
            )
            data = r.json()
            names = [
                a["name"]
                for a in data.get("similarartists", {}).get("artist", [])
            ][:10]
            cache.set(key, names, TTL_LASTFM_SIMILAR)
            return names
    except Exception:
        return []


def build_feature_vector(
    deezer_data: dict | None,
    tags: list[str],
    genres: list[str],
    vibes: list[str],
) -> np.ndarray:
    """Build a 28-dim L2-normalised feature vector.

    Layout:
      [0:5]   audio axes  (energy, danceability, valence, bpm_tier, darkness)
      [5:20]  genre one-hot × 0.4
      [20:28] vibe  one-hot × 0.3
    """
    axes = {"energy": 0.0, "danceability": 0.0, "valence": 0.0, "bpm_tier": 0.0, "darkness": 0.0}

    for tag in tags:
        contrib = VIBE_TAGS.get(tag)
        if contrib:
            for axis, weight in contrib.items():
                axes[axis] += weight

    if deezer_data:
        bpm = deezer_data.get("bpm", 0) or 0
        gain = deezer_data.get("gain", 0) or 0
        if bpm > 0:
            axes["bpm_tier"] += float(np.clip((bpm - 80) / 120, 0, 1))
        if gain != 0:
            axes["energy"] += float(np.clip((-gain - 3) / 12, 0, 1))

    audio_vec = np.array([
        float(np.clip(axes["energy"],       -1, 1)),
        float(np.clip(axes["danceability"], -1, 1)),
        float(np.clip(axes["valence"],      -1, 1)),
        float(np.clip(axes["bpm_tier"],     -1, 1)),
        float(np.clip(axes["darkness"],     -1, 1)),
    ])

    genre_vec = np.zeros(15)
    genres_lower = [g.lower() for g in genres]
    for i, canonical in enumerate(GENRE_LIST):
        if any(canonical in gl or gl in canonical for gl in genres_lower):
            genre_vec[i] = 0.4

    vibe_vec = np.zeros(8)
    for i, v in enumerate(VIBE_LIST):
        if v in vibes:
            vibe_vec[i] = 0.3

    vec = np.concatenate([audio_vec, genre_vec, vibe_vec])
    norm = np.linalg.norm(vec)
    if norm > 0:
        vec = vec / norm
    return vec
