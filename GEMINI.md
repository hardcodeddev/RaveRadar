# RaveRadar

RaveRadar is a full-stack application for discovering and tracking music events, raves, and artists.

## 🚀 Deployment Guide (Simplified Docker)

This project is dockerized to run both the **Backend** and **Frontend** in a single container. This is the easiest way to deploy to **Render**.

### 1. Render Deployment (Web Service)

1.  **Create a Render Account:** Go to [render.com](https://render.com).
2.  **New Web Service:** Connect your GitHub repository.
3.  **Configure:**
    *   **Runtime:** `Docker`
    *   **Region:** Choose the one closest to you.
    *   **Instance Type:** Free (or any tier).
4.  **Environment Variables:**
    *   `Spotify__ClientId`: Your Client ID.
    *   `Spotify__ClientSecret`: Your Client Secret.
    *   `EdmTrain__ApiKey`: Your EDMTrain API Key.
    *   `ASPNETCORE_ENVIRONMENT`: `Production`
5.  **Persistent Disk (Crucial for SQLite):**
    *   Go to the **Disk** tab in your Render service.
    *   **Mount Path**: `/app/data`
    *   **Size**: 1GB (Free).
    *   *Note: This ensures your users and favorites aren't deleted when the server restarts.*

### 2. That's it!
Render will build the Docker image (which compiles the React frontend and the .NET API) and serve them together at `https://your-app-name.onrender.com`.

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
