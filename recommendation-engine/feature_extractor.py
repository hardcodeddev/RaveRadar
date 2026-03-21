import asyncio
import numpy as np
import httpx
from cache import cache, TTL_DEEZER, TTL_MUSICBRAINZ
from vibe_tags import VIBE_TAGS

_MB_HEADERS = {
    "User-Agent": "RaveRadar/1.0 (raveradar@local.dev)",
    "Accept": "application/json",
}

# Semaphore created lazily on first async call (one event loop in FastAPI)
_mb_sem: asyncio.Semaphore | None = None

def _get_mb_sem() -> asyncio.Semaphore:
    global _mb_sem
    if _mb_sem is None:
        _mb_sem = asyncio.Semaphore(1)
    return _mb_sem

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


async def fetch_musicbrainz_tags(artist: str, song: str) -> list[str]:
    """Fetch community tags from MusicBrainz (no API key required).

    Two-step: search recording → MBID, then lookup tags for that recording.
    Requests are serialised to 1/sec to respect MusicBrainz rate limits.
    Results are cached for 12 h so cold-start cost only occurs once.
    """
    key = f"mb:tags:{artist.lower()}:{song.lower()}"
    cached = cache.get(key)
    if cached is not None:
        return cached

    sem = _get_mb_sem()
    try:
        async with httpx.AsyncClient(timeout=8.0) as client:
            # Step 1 — find recording MBID
            async with sem:
                r1 = await client.get(
                    "https://musicbrainz.org/ws/2/recording/",
                    headers=_MB_HEADERS,
                    params={
                        "query": f'artist:"{artist}" AND recording:"{song}"',
                        "fmt": "json",
                        "limit": 1,
                    },
                )

            recordings = r1.json().get("recordings", [])
            if not recordings:
                cache.set(key, [], TTL_MUSICBRAINZ)
                return []

            mbid = recordings[0]["id"]

            # Step 2 — fetch tags for that recording
            async with sem:
                r2 = await client.get(
                    f"https://musicbrainz.org/ws/2/recording/{mbid}",
                    headers=_MB_HEADERS,
                    params={"inc": "tags", "fmt": "json"},
                )

            raw_tags = r2.json().get("tags", [])
            tags = [
                t["name"].lower()
                for t in sorted(raw_tags, key=lambda x: x.get("count", 0), reverse=True)
            ][:10]

            cache.set(key, tags, TTL_MUSICBRAINZ)
            return tags

    except Exception:
        cache.set(key, [], TTL_MUSICBRAINZ)
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
