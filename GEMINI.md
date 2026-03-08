# RaveRadar

RaveRadar is a full-stack application for discovering and tracking music events, raves, and artists.

## 🚀 Deployment Guide (Pain-Free)

This project is designed to be deployed for free using **Render** (Backend) and **GitHub Pages** (Frontend).

### 1. Backend Deployment (Render)

1. **Create a Render Account:** Go to [render.com](https://render.com).
2. **New Web Service:** Connect your GitHub repository.
3. **Configure:**
   - **Runtime:** `Docker` (or `Web Service` if using .NET runtime).
   - **Build Command:** `dotnet publish -c Release -o out`
   - **Start Command:** `dotnet out/RaveRadar.Api.dll`
   - **Disk (Recommended for Persistence):** To keep your SQLite database (`RaveRadar.db`) persistent across restarts:
     - Add a **Render Disk** (e.g., 1GB, free tier).
     - **Mount Path:** `/app/data`
     - **Update Connection String:** Set `ConnectionStrings__DefaultConnection` to `Data Source=/app/data/RaveRadar.db`.

4. **Environment Variables (Security First!):**
   > ⚠️ **IMPORTANT:** Never commit secrets to your repository. Use Render's Environment Variables dashboard to keep them secure.

   | Key | Description |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |
   | `EdmTrain__ApiKey` | Your [EDMTrain API Key](https://edmtrain.com/api-documentation). |
   | `Spotify__ClientId` | Your [Spotify Developer](https://developer.spotify.com/dashboard) Client ID. |
   | `Spotify__ClientSecret` | Your Spotify Client Secret. |
   | `SoundCloud__ClientId` | (Optional) Your SoundCloud Client ID. |
   | `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/RaveRadar.db` |

### 2. OAuth & Callback Links

If you enable features requiring Spotify user authentication (like "On Repeat" sync), you must configure your **Redirect URIs** in the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard):

- **Development:** `http://localhost:5057/auth/spotify/callback`
- **Production:** `https://<your-render-app-url>.onrender.com/api/auth/spotify/callback`

### 3. Frontend Deployment (GitHub Pages)

1. **Update API Base URL:** In `RaveRadar.Client/src/services/api.ts`, change `baseURL` to your Render service URL (e.g., `https://raveradar-api.onrender.com/api`).
2. **Install GH-Pages:** `cd RaveRadar.Client && npm install gh-pages --save-dev`
3. **Update package.json:** Add `"homepage": "https://<your-username>.github.io/RaveRadar"` to `package.json`.
4. **Deploy:** `npm run deploy` (Ensure you have a `deploy` script: `"deploy": "gh-pages -d dist"`).

---

## Features implemented

- **Artist Discovery:** Search over 500+ modern EDM artists (Levity, John Summit, etc.).
- **Smart Recommendations:** Events scored by your location, favorite artists, and genres.
- **Dynamic Profile:** Update your city, favorite artists, and "On Repeat" songs as your taste evolves.
- **EDM Train Sync:** Automated background job pulls fresh raves every 12 hours.
- **Modern UI:** Dark mode aesthetic with interactive tags and favorite toggles.

## Tech Stack

- **Backend:** ASP.NET Core 8.0, Entity Framework Core, SQLite, Quartz.NET.
- **Frontend:** React 19, Vite, TypeScript, Axios.
- **Data:** Custom EDM dataset + EDMTrain API integration.

## Development

```bash
# Root
npm install && npm start

# Backend
cd RaveRadar.Api && dotnet run

# Frontend
cd RaveRadar.Client && npm run dev
```
