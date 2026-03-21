# Recommendation Engine — Developer Runbook

This document explains how the ML recommendation engine works, where every number comes from, and how to modify it safely. Read this before editing any file in `recommendation-engine/`.

---

## Overview

The engine is a standalone Python FastAPI process. The .NET backend calls it at `POST /recommend` and uses the result to sort artists for the Discover page. If the engine is unreachable or returns an error, the backend silently falls back to the genre-matching algorithm — the engine is always optional.

```
User visits /discover
  └─ GET /api/Users/{id}/recommendations  (C#)
       ├─ Tries: POST http://localhost:8000/recommend
       │    ├─ For each saved track + each candidate artist:
       │    │    ├─ GET https://api.deezer.com/search   → BPM, loudness
       │    │    └─ GET https://ws.audioscrobbler.com/  → community tags
       │    ├─ Build 28-dim feature vector per item
       │    ├─ Build user profile (recency-weighted centroid)
       │    └─ Score candidates by cosine similarity + bonuses
       └─ Falls back to: genre-overlap sort (existing code, untouched)
```

---

## File Map

| File | What it does | Edit when you want to... |
|---|---|---|
| `vibe_tags.py` | Maps Last.fm community tags to 5 audio axes | Add a new tag, change how a tag influences the score |
| `cache.py` | In-process TTL dict (no Redis) | Change cache TTLs |
| `feature_extractor.py` | Fetches Deezer + Last.fm, builds the feature vector | Change which Deezer fields are used, change genre/vibe lists, change vector weighting |
| `recommender.py` | Builds user profile, scores candidates, generates reason strings | Change scoring bonuses, change how recency is weighted, change reason language |
| `models.py` | Pydantic request/response schemas | Add a new field to the API request or response |
| `main.py` | FastAPI app, endpoint handlers | Add a new endpoint, change candidate cap, change fallback behaviour |

---

## The Feature Vector

Every track and every candidate artist is represented as a **28-dimensional vector**. All vectors are **L2-normalised** before use (divided by their Euclidean length so they sit on the unit sphere). This makes cosine similarity equivalent to a dot product, which is fast.

### Segment layout

```
Index  Segment         Source                      Weight
─────  ──────────────  ──────────────────────────  ──────
0      energy          Last.fm tags + Deezer gain  raw (clamped [-1,1])
1      danceability    Last.fm tags                raw (clamped [-1,1])
2      valence         Last.fm tags                raw (clamped [-1,1])
3      bpm_tier        Last.fm tags + Deezer BPM   raw (clamped [-1,1])
4      darkness        Last.fm tags                raw (clamped [-1,1])
5–19   genre one-hot   genres list from DB         0.4 per match
20–27  vibe one-hot    vibes list from DB          0.3 per match
```

### Audio axes (indices 0–4)

These are accumulated by adding up the weights from every Last.fm tag the track has, then clamped to [-1, 1].

Example: a track tagged `["dark", "energetic", "heavy"]` accumulates:
- energy: `0.9 + 0.6 = 1.5` → clamped to `1.0`
- danceability: `0.5 + 0.4 = 0.9`
- valence: `-0.6`
- darkness: `0.7 + 0.4 = 1.1` → clamped to `1.0`

Two audio signals come directly from Deezer (not tags):

**BPM normalisation** (`axes["bpm_tier"] +=`):
```python
(bpm - 80) / 120   # clipped [0, 1]
```
- BPM 80 → 0.0, BPM 140 → 0.5, BPM 200 → 1.0
- Techno/hardstyle (140–160 BPM) maps to ~0.5–0.67
- Drum & Bass (170–180 BPM) maps to ~0.75–0.83

**Energy from loudness** (`axes["energy"] +=`):
```python
(-gain - 3) / 12   # clipped [0, 1]
```
- Deezer's `gain` field is negative dBFS (louder tracks have more negative values)
- A gain of `-3` → 0.0 (neutral)
- A gain of `-15` → 1.0 (very loud = high energy)

### Genre segment (indices 5–19)

The 15 canonical genres in order:
```
0  house        5  dubstep      10  hardstyle
1  tech house   6  drum & bass  11  ambient
2  bass house   7  trance       12  progressive
3  techno       8  trap         13  psytrance
4  hard techno  9  future bass  14  electro
```

Matching is substring-based (e.g. `"tech house"` matches a genre string of `"Tech House"` or `"tech-house"`). When a genre matches, the corresponding slot is set to **0.4** (not 1.0, because genre is a weaker signal than audio features).

**To add a genre:** append it to `GENRE_LIST` in `feature_extractor.py`. The vector length increases by 1, which will break any persisted/cached vectors — clear the in-process cache by restarting the engine.

### Vibe segment (indices 20–27)

The 8 vibes, in the same order as `SpotifyService.DeriveVibes()` in C#:
```
0  Dark        4  Energetic
1  Groovy      5  Chill
2  Bass Heavy  6  Trippy
3  Euphoric    7  Festival
```

These come from the `Vibes` field on `SavedTrack` and `Artist`, which is populated by `DeriveVibes()` in the C# backend. Matching is exact string comparison. When present, set to **0.3**.

**If you add a vibe in `DeriveVibes()` (C#):** add the same string to `VIBE_LIST` in `feature_extractor.py` in the same relative position. Order matters — the vector layout must be consistent between all calls.

---

## The Scoring Algorithm

### Step 1: Build the user profile

The user profile is a single 28-dim vector representing the user's taste.

**With 3+ saved tracks** (`recommender.py: build_user_profile`):
```
weight_i = exp(-0.02 * age_in_days)
profile  = sum(weight_i * vector_i) / sum(weight_i)
profile  = profile / ||profile||   # L2-normalise
```

The decay constant `0.02` means a track saved 35 days ago has roughly half the weight of one saved today. To make the engine more or less recency-sensitive, change this constant:
- Higher (e.g. `0.05`) → recent taste matters much more
- Lower (e.g. `0.005`) → long-term taste matters more

**With fewer than 3 saved tracks:** the engine uses the feature vectors of the user's favorite artists (matched from the candidate pool) as a proxy profile, averaged and normalised.

### Step 2: Score each candidate

For each candidate artist (capped to top 20 by Spotify popularity):

```
base_score = dot(user_profile, candidate_vector)   # cosine similarity, range ~[-1, 1]

# Bonuses
if |candidate_bpm - user_bpm| <= 10:   score += 0.05
if |candidate_energy - user_energy| <= 0.15:   score += 0.03
if dot(user_vibes, candidate_vibes) > 0:   score += 0.03

# Penalty
if artist_bpm_std > 30:   score -= 0.05   # incoherent/genre-diverse artist
```

The user BPM is reconstructed from the profile's `bpm_tier` value: `bpm = bpm_tier * 120 + 80`.

Currently `bpm_std` is always `0.0` (it would require multiple tracks per artist to compute). The penalty is wired up and ready to use if you add per-artist track fetching.

### Step 3: Generate the reason string

The reason string is built from the top 2 audio axes where `user_profile[i] * candidate_vector[i]` is largest (strongest shared signal):

```python
AXIS_PHRASES = {
    "energy":       ("high-energy",  "low-energy"),
    "danceability": ("dancefloor",   "ambient"),
    "valence":      ("euphoric",     "dark"),
    "bpm_tier":     ("fast-paced",   "downtempo"),
    "darkness":     ("dark",         "bright"),
}
```

If `contributions[i] > 0`, the positive phrase is used; if `< 0`, the negative phrase. The result is joined with a space and suffixed with `" taste"`:

> `"high-energy dancefloor taste"`

**To change the language:** edit `AXIS_PHRASES` in `recommender.py`. **To add more variety:** modify `_make_reason()` to include genre or vibe information from the remaining vector segments.

---

## The Tag Map (`vibe_tags.py`)

This is the most impactful file to tune. Each key is a Last.fm community tag (lowercase). Each value is a dict of `axis → contribution`. Contributions are additive and unclamped until `build_feature_vector` clamps them to [-1, 1].

### Current tags

| Tag | energy | danceability | valence | bpm_tier | darkness |
|---|---|---|---|---|---|
| energetic | +0.9 | +0.5 | | | |
| dark | | | -0.6 | | +0.7 |
| euphoric | +0.5 | | +0.9 | | |
| dance | +0.3 | +0.9 | | | |
| ambient | -0.8 | -0.5 | | | |
| uplifting | +0.4 | | +0.8 | | |
| aggressive | +0.7 | +0.3 | | | +0.5 |
| groovy | | +0.8 | +0.4 | | |
| melodic | | +0.3 | +0.6 | | |
| heavy | +0.6 | +0.4 | | | +0.4 |
| chill | -0.7 | -0.3 | +0.3 | | |
| underground | +0.3 | | | | +0.4 |
| trippy | | +0.4 | +0.5 | | +0.3 |
| psychedelic | +0.5 | | +0.4 | | +0.3 |
| hard | +0.8 | +0.5 | | | +0.3 |
| minimal | -0.2 | +0.2 | | | |
| peak time | +0.8 | +0.7 | | | |
| driving | +0.6 | +0.6 | | +0.4 | |
| fast | +0.5 | | | +0.8 | |
| slow | -0.3 | | | -0.5 | |
| bass | +0.5 | +0.6 | | | +0.2 |
| industrial | +0.6 | +0.2 | | | +0.8 |
| happy | +0.5 | +0.5 | +0.9 | | |
| sad | -0.3 | | -0.7 | | +0.3 |
| romantic | -0.2 | +0.3 | +0.7 | | |

### How to add a new tag

1. Look up the exact lowercase string that Last.fm uses (search the tag on last.fm)
2. Decide which axes it influences and by how much (values between -1.0 and +1.0)
3. Add the entry to `VIBE_TAGS` in `vibe_tags.py`

```python
"melodic techno": {"energy": 0.5, "danceability": 0.4, "darkness": 0.3},
```

The tag will be picked up automatically on the next request — no cache to clear unless you want to force re-fetching existing track tags (in which case restart the engine).

### How to tune an existing tag

Change the weight values directly. Increases over 1.0 are fine — they just get clamped if combined with other tags that push the same axis. A good approach is to test with the curl command at the bottom of this document.

---

## The Cache (`cache.py`)

In-process dict with TTL. Cache is **per-process** and **lost on restart**. There is no Redis or shared cache.

| Cache key format | TTL | Holds |
|---|---|---|
| `deezer:{artist}:{title}` | 24 hours | `{bpm, gain, duration, rank}` |
| `lastfm:tags:{artist}:{title}` | 6 hours | list of tag strings |
| `lastfm:similar:{artist}` | 12 hours | list of artist name strings |

**To change a TTL:** edit the constants at the bottom of `cache.py`:
```python
TTL_DEEZER = 24 * 3600
TTL_LASTFM_TAGS = 6 * 3600
TTL_LASTFM_SIMILAR = 12 * 3600
```

**To force a cache miss during testing:** restart the engine (`Ctrl+C` then `uvicorn main:app --reload --port 8000`). With `--reload`, uvicorn does NOT clear the in-process cache on file change — only a full restart clears it.

---

## Common Modifications

### Raise or lower the candidate cap

In `main.py`, the engine sorts candidates by Spotify popularity and takes the top 20:

```python
top_candidates = sorted(req.candidate_artists, key=lambda a: a.popularity, reverse=True)[:20]
```

Change `[:20]` to a larger or smaller number. Each candidate generates one Deezer request and one Last.fm request (when Last.fm key is set), so `n` candidates = `n * 2` external HTTP calls (minus cache hits). At 20 candidates and no cache, expect ~40 requests, each with up to a 5s timeout, run in parallel.

### Change the recency decay rate

In `recommender.py`:

```python
weights.append(float(np.exp(-0.02 * age_days)))
```

The constant `0.02` is the decay rate. Table of half-lives:

| Constant | Half-life |
|---|---|
| 0.005 | ~139 days |
| 0.01 | ~69 days |
| 0.02 | ~35 days (current) |
| 0.05 | ~14 days |
| 0.1 | ~7 days |

### Change scoring bonuses

In `recommender.py`, inside `score_candidates`:

```python
# Current bonuses — change the addend values
if |bpm delta| <= 10:   score += 0.05
if |energy delta| <= 0.15:   score += 0.03
if vibe overlap > 0:   score += 0.03
if bpm_std > 30:   score -= 0.05   # penalty
```

The base cosine similarity sits in the range [-1, 1] but in practice for EDM tracks it'll be [0.2, 0.9]. A +0.05 bonus is roughly equivalent to moving a candidate up 1–2 positions.

### Add a new scoring signal

1. Add the new data to the `candidate_data` list in `main.py` — the dict passed to `score_candidates`
2. Read it inside `score_candidates` in `recommender.py` and apply a bonus or penalty

### Change the reason string

Edit `AXIS_PHRASES` in `recommender.py` to change the words. To surface more context (e.g. mention the genre), modify `_make_reason()`:

```python
def _make_reason(user_profile, candidate_vector):
    # current: looks at indices 0-4 (audio axes only)
    # to include genre: inspect indices 5-19 and append the top matching genre name
```

### Add a new endpoint

Add a new `@app.post(...)` or `@app.get(...)` function to `main.py`. Add a corresponding Pydantic model to `models.py` if needed. Call it from the C# `RecommendationEngineService.cs`.

---

## C# Integration Points

### `RecommendationEngineService.cs`

```
RaveRadar.Api/Services/RecommendationEngineService.cs
```

Wraps all HTTP communication with the Python engine. Any exception (connection refused, timeout, 5xx) returns `null` and the caller falls through to the genre-based fallback. The timeout is **10 seconds** — long enough for a cold start but short enough not to block a user.

Two methods:
- `GetRecommendations(user, candidateArtists)` — called by `GET /api/Users/{id}/recommendations`
- `GetTrackFeatures(artistName, songName)` — called fire-and-forget on every `POST /saved-tracks`

### `UsersController.cs` — recommendation flow

```csharp
// 1. Load allPool (all artists not in user's favorites)
// 2. Attempt ML engine → returns RecommendEngineResult or null
// 3. If non-null: sort allPool by ML scores, build response, cache, return
// 4. If null: fall through to existing genre-matching block (unchanged)
```

The recommendations are cached in `IMemoryCache` for **30 minutes** per user (`cacheKey = "recs:{userId}"`). To bust the cache during development without restarting, temporarily remove or comment out the `_cache.Set(...)` call.

### Adding a new field from Python to C#

1. Add the field to the Python `AudioFeatureDto` or `ScoredArtist` in `models.py`
2. Add the matching `[JsonPropertyName("snake_case_name")]` property to the C# DTO in `RecommendationEngineService.cs`
3. The JSON deserializer handles the snake_case → PascalCase mapping automatically via the attribute

---

## Testing

### Manual curl test (engine running on 8000)

```bash
curl -X POST http://localhost:8000/recommend \
  -H "Content-Type: application/json" \
  -d '{
    "user_id": 1,
    "saved_tracks": [
      {
        "artist_name": "Skrillex",
        "song_name": "Scary Monsters and Nice Sprites",
        "genres": ["dubstep", "bass music"],
        "vibes": ["Bass Heavy", "Energetic"],
        "added_at": "2026-03-01T00:00:00Z"
      },
      {
        "artist_name": "Excision",
        "song_name": "Throwin Elbows",
        "genres": ["dubstep", "bass"],
        "vibes": ["Bass Heavy", "Dark"],
        "added_at": "2026-03-10T00:00:00Z"
      },
      {
        "artist_name": "Virtual Riot",
        "song_name": "Energy Drink",
        "genres": ["dubstep"],
        "vibes": ["Bass Heavy"],
        "added_at": "2026-03-15T00:00:00Z"
      }
    ],
    "favorite_artist_names": ["Skrillex"],
    "candidate_artists": [
      {
        "id": 10,
        "name": "Rezz",
        "genres": ["techno", "dark techno"],
        "vibes": ["Dark", "Trippy"],
        "top_tracks": ["Edge"],
        "popularity": 75
      },
      {
        "id": 11,
        "name": "Dom Dolla",
        "genres": ["tech house"],
        "vibes": ["Groovy"],
        "top_tracks": ["Saving Up"],
        "popularity": 91
      }
    ]
  }'
```

Expected shape of response:
```json
{
  "artists": [
    {"id": 10, "score": 0.72, "reason": "high-energy dark taste"},
    {"id": 11, "score": 0.41, "reason": "dancefloor taste"}
  ],
  "source": "ml"
}
```

### Test track features

```bash
curl -X POST http://localhost:8000/track-features \
  -H "Content-Type: application/json" \
  -d '{"artist_name": "Skrillex", "song_name": "Scary Monsters and Nice Sprites"}'
```

### Check engine health

```bash
curl http://localhost:8000/health
# {"status":"ok"}
```

### Interactive API docs

While the engine is running: `http://localhost:8000/docs` (FastAPI auto-generates Swagger UI).

---

## What Happens Without a Last.fm Key

Without `LASTFM_API_KEY`, `fetch_lastfm_tags` returns `[]` immediately (no HTTP call made). The feature vector will have:
- Audio axes from Deezer's BPM and gain only (indices 0 and 3)
- Genre one-hot intact (from the C# DB data)
- Vibe one-hot intact (from `DeriveVibes()` in C#)

Genre and vibe segments alone are enough to produce useful recommendations. The reason strings will default to `"matches your taste"` if the audio axis contributions are too small to determine a dominant signal.

---

## What Happens When Deezer Returns No BPM

Many tracks on Deezer have `"bpm": 0`. When this happens:
- `bpm_tier` receives no contribution from Deezer (Last.fm tags like `"fast"` or `"driving"` can still contribute to it)
- `energy` receives no contribution from gain
- The BPM proximity bonus in scoring is skipped for that candidate (`candidate_bpm = 0`)

This is normal and expected. Do not treat a zero BPM as an error.

---

## Production Behaviour

In production (Docker), both processes run under `supervisord`:

```ini
[program:dotnet]   command=dotnet /app/RaveRadar.Api.dll
[program:engine]   command=python3 -m uvicorn main:app --host 0.0.0.0 --port 8000
```

Port 8000 is **not exposed** in the `Dockerfile` — it is only reachable internally on `localhost` by the .NET process. This is intentional. The engine has no authentication and should never be public-facing.

If the engine crashes (out of memory, unhandled exception), supervisord restarts it automatically. During the restart window (typically a few seconds), recommendation requests fall back to genre-matching. No user-visible error occurs.

---

## Glossary

| Term | Meaning |
|---|---|
| Feature vector | A 28-number array representing a track or artist's musical character |
| L2-normalised | Divided by Euclidean length so the vector sits on the unit sphere |
| Cosine similarity | Dot product of two L2-normalised vectors. 1.0 = identical direction, 0 = orthogonal, -1 = opposite |
| User profile | The recency-weighted average of all the user's saved-track vectors, normalised |
| Audio axis | One of the 5 scalar dimensions (energy, danceability, valence, bpm_tier, darkness) |
| Vibe | A high-level mood label derived from genre in C# (`SpotifyService.DeriveVibes`) |
| Gain | Deezer's loudness field. Negative dBFS — more negative = louder |
| bpm_tier | Normalised BPM: `(bpm - 80) / 120`. Range [0, 1] for BPM 80–200 |
