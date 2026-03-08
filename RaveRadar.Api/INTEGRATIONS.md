# RaveRadar — Third-Party API Integration Guide

This document explains how to wire up Spotify and SoundCloud APIs to replace placeholder data with real tracks, real artist images, and real audio previews.

---

## Spotify Web API

### What you get
- Real artist images, bios, and popularity scores
- Actual top tracks per artist
- Audio preview URLs (30s clips)
- Search across Spotify's full catalog

### Step 1 — Create an app

1. Go to [developer.spotify.com/dashboard](https://developer.spotify.com/dashboard)
2. Click **Create app**
3. Set a redirect URI — for local dev: `http://localhost:5057/auth/spotify/callback`
4. Copy your **Client ID** and **Client Secret**

### Step 2 — Add credentials to `appsettings.json`

```json
"Spotify": {
  "ClientId": "your_client_id_here",
  "ClientSecret": "your_client_secret_here"
}
```

> Never commit real credentials. Use `dotnet user-secrets` or environment variables in production.

### Step 3 — Get an access token (Client Credentials flow)

For read-only data (no user auth needed), use the Client Credentials flow:

```csharp
// Services/SpotifyService.cs
public class SpotifyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public SpotifyService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    private async Task<string> GetAccessToken()
    {
        var client = _httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config["Spotify:ClientId"]}:{_config["Spotify:ClientSecret"]}")
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }
}
```

### Step 4 — Key endpoints

#### Search for an artist
```
GET https://api.spotify.com/v1/search?q={name}&type=artist&limit=1
Authorization: Bearer {token}
```
Returns: `id`, `name`, `images[]`, `popularity`, `genres[]`

#### Get an artist's top tracks
```
GET https://api.spotify.com/v1/artists/{spotifyId}/top-tracks?market=US
Authorization: Bearer {token}
```
Returns: tracks with `name`, `preview_url` (30s audio), `external_urls.spotify`

#### Search for a track
```
GET https://api.spotify.com/v1/search?q={query}&type=track&limit=10
Authorization: Bearer {token}
```

### Step 5 — Where to integrate

**`DbSeeder.cs` / `ArtistsController`** — After seeding artists, call `SpotifyService.EnrichArtist(name)` to fill in `SpotifyId`, `ImageUrl`, `Popularity`, and `TopTracks`.

**`ArtistsController.cs` — `GET /api/Artists/songs/search`** — Replace the in-memory TopTracks search with a live Spotify track search:
```csharp
[HttpGet("songs/search")]
public async Task<IActionResult> SearchSongs([FromQuery] string q)
{
    var results = await _spotifyService.SearchTracks(q);
    return Ok(results);
}
```

**Recommended `SpotifyService` methods to build:**
| Method | Purpose |
|---|---|
| `SearchArtist(string name)` | Returns artist metadata |
| `GetTopTracks(string spotifyId)` | Returns top 5 tracks with preview URLs |
| `SearchTracks(string query)` | Powers the song search autocomplete |
| `GetRelatedArtists(string spotifyId)` | Powers the Discover recommendations tab |

---

## SoundCloud API

### What you get
- Full-length audio streams (where permitted)
- User-uploaded tracks and mixes
- Artist profiles and follower counts
- Playlists / sets

### Note on access
SoundCloud's public API v2 is partially restricted. For production use, apply for API access at [developers.soundcloud.com](https://developers.soundcloud.com). For development, the public search endpoint works without a key for basic testing.

### Step 1 — Register your app

1. Go to [developers.soundcloud.com](https://developers.soundcloud.com)
2. Register an application
3. Note your **Client ID**

### Step 2 — Add credentials to `appsettings.json`

```json
"SoundCloud": {
  "ClientId": "your_soundcloud_client_id"
}
```

### Step 3 — Key endpoints

#### Search tracks
```
GET https://api.soundcloud.com/tracks?q={query}&client_id={clientId}&limit=10
```
Returns: `id`, `title`, `user.username`, `artwork_url`, `stream_url`, `permalink_url`

#### Search users (artists)
```
GET https://api.soundcloud.com/users?q={name}&client_id={clientId}&limit=5
```

#### Stream a track
```
GET {stream_url}?client_id={clientId}
```
This returns a direct audio stream URL.

### Step 4 — Where to integrate

**`Services/SoundCloudService.cs`** — Build a service similar to SpotifyService:
```csharp
public class SoundCloudService
{
    public async Task<List<SongResult>> SearchTracks(string query) { ... }
    public async Task<string?> GetStreamUrl(string trackUrl) { ... }
}
```

**`ArtistsController` — song search** — Merge results from both Spotify and SoundCloud:
```csharp
var spotifyResults = await _spotifyService.SearchTracks(q);
var soundcloudResults = await _soundCloudService.SearchTracks(q);
var merged = spotifyResults.Concat(soundcloudResults).Take(20).ToList();
return Ok(merged);
```

**`SongResult` model** — Add `previewUrl` and `source` fields to surface playback:
```csharp
public class SongResult
{
    public int ArtistId { get; set; }
    public required string ArtistName { get; set; }
    public required string SongName { get; set; }
    public string? ImageUrl { get; set; }
    public string? PreviewUrl { get; set; }    // 30s Spotify preview or SC stream
    public string? ExternalUrl { get; set; }   // deep link to Spotify/SC
    public string? Source { get; set; }        // "Spotify" | "SoundCloud"
}
```

---

## Recommended Implementation Order

1. **Spotify Client Credentials flow** — unblocks artist enrichment and song search with no user login required
2. **Artist enrichment job** — run as a Quartz background job after `DbSeeder`, fill in real images and top tracks
3. **Song search via Spotify** — replace the `TopTracks` substring search with live Spotify search
4. **SoundCloud track search** — add as a secondary source, merge results in the song search endpoint
5. **`getRelatedArtists` for Discover** — use `GET /v1/artists/{id}/related-artists` from Spotify to power the recommendation engine instead of genre matching

---

## DI Registration (Program.cs)

```csharp
// Register new services
builder.Services.AddScoped<SpotifyService>();
builder.Services.AddScoped<SoundCloudService>();
```

---

## Rate Limits

| Service | Limit |
|---|---|
| Spotify | 100 req/s (Client Credentials) |
| SoundCloud | ~300 req/hr (public) |

Cache token responses and consider adding a simple in-memory result cache (e.g., `IMemoryCache`) for popular search queries to stay well within limits.
