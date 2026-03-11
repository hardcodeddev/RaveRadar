# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RaveRadar is a full-stack EDM event discovery platform. The backend is ASP.NET Core 8.0, the frontend is React 19 + Vite + TypeScript, and they are served together from one Docker container in production.

## Commands

### Development (run both services concurrently)
```bash
npm install          # Install root + client deps (dotnet restore + npm install)
npm start            # Start backend (port 5000) + frontend (port 5173) concurrently
```

### Backend only
```bash
cd RaveRadar.Api
dotnet run           # http://localhost:5000
dotnet build
dotnet test
```

### Frontend only
```bash
cd RaveRadar.Client
npm run dev          # http://localhost:5173
npm run build        # Production build → dist/
npm run lint         # ESLint
npm run preview      # Preview production build
```

### Docker (production-like)
```bash
docker build -t raveradar .
docker run -p 8080:8080 \
  -e Spotify__ClientId=... \
  -e Spotify__ClientSecret=... \
  -e EdmTrain__ApiKey=... \
  raveradar
```

## Architecture

```
RaveRadar.Client/   ← React 19 + Vite + TypeScript frontend
RaveRadar.Api/      ← ASP.NET Core 8.0 backend
```

### Backend (`RaveRadar.Api/`)

- **Program.cs** — Startup: DI registration, CORS, Quartz jobs, DB migration/seeding, Swagger, static files
- **Controllers/** — REST endpoints: `ArtistsController`, `EventsController`, `UsersController`, `GenresController`
- **Services/** — `SpotifyService` (artist enrichment + song search via client credentials OAuth), `EdmTrainService` (event sync)
- **Data/AppDbContext.cs** — EF Core context; supports SQLite (default dev) and PostgreSQL (via `DATABASE_URL` env var)
- **Quartz/** — Background jobs: `EdmTrainSyncJob` (every 12h), `SpotifyEnrichJob` (every 24h, runs at startup)
- **Models/** — EF entities: `User`, `Artist`, `Event`, `Genre`, `SavedTrack`

Database is auto-migrated and seeded on startup. Seed data comes from `edm_artists_dataset.txt` (500+ EDM artists).

### Frontend (`RaveRadar.Client/src/`)

- **App.tsx** — React Router setup; routes: `/` (Dashboard), `/discover`, `/preferences`, `/login`, `/register`
- **pages/** — `Dashboard`, `DiscoverPage`, `PreferencesPage`, `LoginPage`, `RegisterPage`
- **services/api.ts** — Axios client with typed API calls for all endpoints
- **AuthContext.tsx** — User session (login/logout/persist)
- **models.ts** — TypeScript interfaces: `User`, `Artist`, `Event`, `SavedTrack`

In production, the React build is served as static files from the backend's `wwwroot/`.

### Key API Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/Artists` | Search/filter artists |
| GET | `/api/Artists/songs/search` | Live Spotify song search (falls back to local) |
| GET | `/api/Events` | Events by city or user recommendations |
| GET | `/api/Users/{id}/recommendations` | Scored recommendations (location + favorites) |
| POST | `/api/Users/register` / `/login` | Auth (BCrypt, no JWT — userId returned) |
| POST | `/api/Users/{id}/preferences` | Update location, artists, genres, songs |
| GET | `/health` | Health check |
| GET | `/api` | API info + database provider |

Swagger UI is available at `/api/swagger` in development.

## Database

- **Dev default:** SQLite (`RaveRadar.db` in project root)
- **Production:** PostgreSQL via `DATABASE_URL` env var (`postgres://user:pass@host:port/db`)
- Migrations are applied automatically at startup via `db.Database.MigrateAsync()`

## External Integrations

- **Spotify** — Client Credentials OAuth (no user login). Configured via `Spotify__ClientId` / `Spotify__ClientSecret` in `appsettings.json` or env vars
- **EdmTrain** — Event data sync. Configured via `EdmTrain__ApiKey`

## Deployment

```
Frontend + Backend  →  Render   (single Docker container)
Database            →  Supabase (PostgreSQL)
```

**Render:**
- Runtime: Docker (uses the repo's `Dockerfile`)
- The React build is bundled into the image and served as static files by the .NET backend
- Env vars: `Spotify__ClientId`, `Spotify__ClientSecret`, `EdmTrain__ApiKey`, `DATABASE_URL`, `ASPNETCORE_ENVIRONMENT=Production`

**Supabase:**
1. Create a new project in Supabase
2. Open SQL Editor and run `supabase-setup.sql` (repo root) — this creates all tables, seeds genres/artists/events, and pre-marks all EF Core migrations as applied
3. Copy the connection string (`postgres://...`) → set as `DATABASE_URL` on Render

**Why the SQL script is required:** The EF Core migrations were generated against SQLite and contain `Sqlite:Autoincrement` annotations that silently break auto-increment primary keys on PostgreSQL. The SQL script creates the schema directly with proper PostgreSQL types (`SERIAL`, `double precision`, `timestamptz`), then inserts all migration IDs into `__EFMigrationsHistory` so the backend's startup `Migrate()` call is a no-op.

**Local development:** `VITE_API_BASE_URL` is unset; Vite proxies `/api` → `http://localhost:5000` automatically (configured in `vite.config.ts`).
