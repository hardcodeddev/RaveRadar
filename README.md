# RaveRadar

EDM event and artist discovery platform. Personalized recommendations powered by a Python ML engine backed by Deezer and Last.fm audio features, with a fallback to genre-matching when the engine is unavailable.

---

## Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core 8.0 (C#) |
| Frontend | React 19 + Vite + TypeScript |
| ML Engine | Python 3.11+ / FastAPI / NumPy |
| Database (dev) | SQLite (auto-created) |
| Database (prod) | PostgreSQL via Supabase |
| Deployment | Render (Docker) + Supabase |

---

## Project Layout

```
RaveRadar/
├── RaveRadar.Api/          # ASP.NET Core backend
│   ├── Controllers/        # REST endpoints
│   ├── Services/           # SpotifyService, EdmTrainService, RecommendationEngineService
│   ├── Models/             # EF Core entities
│   ├── Migrations/         # EF Core migrations
│   └── Data/               # AppDbContext + DbSeeder
├── RaveRadar.Client/       # React frontend
│   └── src/
│       ├── pages/          # Dashboard, DiscoverPage, PreferencesPage, LoginPage, RegisterPage
│       ├── services/       # api.ts (Axios client), models.ts, AuthContext.tsx
│       └── App.css         # All styles
├── recommendation-engine/  # Python ML sidecar
│   ├── main.py             # FastAPI app (port 8000)
│   ├── feature_extractor.py
│   ├── recommender.py
│   ├── vibe_tags.py
│   ├── cache.py
│   ├── models.py
│   └── requirements.txt
├── Dockerfile
├── supervisord.conf        # Production: runs dotnet + uvicorn together
├── supabase-setup.sql      # Run once in Supabase SQL editor to set up prod DB
└── package.json            # Root dev scripts
```

---

## Local Development

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- Python 3.11+

### One-time setup

```bash
# 1. Install all dependencies
dotnet restore RaveRadar.Api
npm install --prefix RaveRadar.Client
pip install -r recommendation-engine/requirements.txt

# 2. Set up environment files
cp recommendation-engine/.env.example recommendation-engine/.env
# Edit recommendation-engine/.env and add your Last.fm API key (optional — engine works without it)
```

### Running locally (all three services)

```bash
npm start
```

This starts three processes concurrently:
- **API** — `dotnet run` on `http://localhost:5000`
- **Client** — Vite dev server on `http://localhost:5173`
- **Engine** — uvicorn on `http://localhost:8000`

The frontend proxies all `/api` requests to the backend automatically (configured in `vite.config.ts`). The ML engine is called internally by the backend only — it is not exposed to the browser.

### Running services individually

```bash
# Backend only
cd RaveRadar.Api && dotnet run

# Frontend only
cd RaveRadar.Client && npm run dev

# ML engine only (with hot-reload)
npm run dev:engine
# or directly:
cd recommendation-engine && uvicorn main:app --reload --port 8000
```

### The engine is optional

If the Python process is not running, the backend automatically falls back to the genre-matching algorithm. You will see this in the API logs:

```
⚠️ RecommendationEngine unavailable: Connection refused
```

No action needed — the app continues working normally.

---

## Environment Variables

### Backend (`RaveRadar.Api/appsettings.json` or environment)

| Variable | Default | Description |
|---|---|---|
| `Spotify__ClientId` | — | Spotify app client ID |
| `Spotify__ClientSecret` | — | Spotify app client secret |
| `EdmTrain__ApiKey` | — | EdmTrain API key for event sync |
| `DATABASE_URL` | — | PostgreSQL URL (overrides SQLite in prod) |
| `RecommendationEngine__Url` | `http://localhost:8000` | URL of the Python ML engine |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Set to `Production` on Render |

### ML Engine (`recommendation-engine/.env`)

| Variable | Required | Description |
|---|---|---|
| `LASTFM_API_KEY` | No | Last.fm API key for community tags. Engine works without it but recommendations are less nuanced (audio axes will be zero unless Deezer has BPM data). |

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| GET | `/health` | Health check |
| GET | `/api` | API info + DB provider |
| GET | `/api/Artists` | Search/filter artists |
| GET | `/api/Artists/songs/search` | Live Spotify song search |
| GET | `/api/Events` | Events by city or user |
| GET | `/api/Users/{id}/recommendations` | Personalized recommendations (ML or genre fallback) |
| POST | `/api/Users/register` | Register new user |
| POST | `/api/Users/login` | Login |
| POST | `/api/Users/{id}/preferences` | Update location, artists, genres |
| POST | `/api/Users/{id}/saved-tracks` | Save a track (triggers async audio enrichment) |
| DELETE | `/api/Users/{id}/saved-tracks/{trackId}` | Remove a saved track |
| GET | `/api/swagger` | Swagger UI (dev only) |

### ML Engine endpoints (internal, port 8000)

| Method | Path | Description |
|---|---|---|
| GET | `/health` | Engine health check |
| POST | `/recommend` | Score candidate artists for a user |
| POST | `/track-features` | Extract audio features for a single track |

---

## Database

**Development:** SQLite (`RaveRadar.db`) — created automatically on first run. No setup needed.

**Production:** PostgreSQL on Supabase.

### Running migrations

Migrations apply automatically on startup via `db.Database.MigrateAsync()`. To add a new migration manually:

```bash
cd RaveRadar.Api
dotnet ef migrations add YourMigrationName
```

### Supabase (production setup)

1. Create a new project in Supabase
2. Open the SQL Editor and run the entire contents of `supabase-setup.sql`
3. Copy the connection string from Supabase → Settings → Database → Connection string (URI format)
4. Set it as the `DATABASE_URL` environment variable on Render

The SQL script creates all tables with proper PostgreSQL types, seeds genres and artists, and pre-marks all EF migrations as applied (preventing the SQLite-style migrations from running on PostgreSQL and breaking auto-increment keys).

---

## Docker / Production

```bash
# Build
docker build -t raveradar .

# Run
docker run -p 8080:8080 \
  -e Spotify__ClientId=... \
  -e Spotify__ClientSecret=... \
  -e EdmTrain__ApiKey=... \
  -e DATABASE_URL=postgres://... \
  -e LASTFM_API_KEY=... \
  raveradar
```

The Docker image runs both the .NET API and the Python ML engine as supervised processes via `supervisord`. Both write to stdout/stderr so logs are visible in `docker logs` and on the Render dashboard.

---

## Deployment (Render + Supabase)

1. Push to GitHub
2. On Render, create a new Web Service → Docker runtime → connect repo
3. Set environment variables: `Spotify__ClientId`, `Spotify__ClientSecret`, `EdmTrain__ApiKey`, `DATABASE_URL`, `LASTFM_API_KEY`, `ASPNETCORE_ENVIRONMENT=Production`
4. Deploy

The React build is bundled into the Docker image and served as static files from the .NET backend's `wwwroot/`. No separate frontend hosting needed.

---

## Recommendations: How It Works

See `recommendation-engine/RUNBOOK.md` for a full technical breakdown. In short:

1. When a user visits `/discover`, the frontend calls `GET /api/Users/{id}/recommendations`
2. The backend tries the ML engine first (`POST http://localhost:8000/recommend`)
3. The engine fetches BPM/loudness from Deezer and community tags from Last.fm for each saved track and each candidate artist, builds a 28-dimensional feature vector for each, computes a recency-weighted user profile, and scores candidates by cosine similarity
4. If the engine is unavailable or returns no results, the backend falls back to genre-overlap scoring
5. On every track save, audio features are asynchronously fetched and stored in the database for future use

---

## Development Tips

- **Recommendations are cached** for 30 minutes per user. To force a fresh run during testing, restart the backend or change the cache key in `UsersController.cs`.
- **The Last.fm key is optional.** Without it, audio axis scores come entirely from Deezer's BPM and gain fields. Recommendations still work but the `reason` strings will be less descriptive.
- **Save at least 3 tracks** for the ML engine to build a proper user profile. With fewer than 3, it falls back to using favorite-artist feature vectors as a proxy.
- **Deezer returns BPM = 0 for many tracks.** This is normal. When BPM is 0, the `bpm_tier` axis contribution from Deezer is skipped (Last.fm tags like "fast" still contribute to it).
