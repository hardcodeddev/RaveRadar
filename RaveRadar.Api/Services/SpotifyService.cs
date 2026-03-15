using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RaveRadar.Api.Models;

namespace RaveRadar.Api.Services;

public class SpotifyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpotifyService> _logger;

    private const string TokenCacheKey = "spotify_access_token";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiBase = "https://api.spotify.com/v1";

    // Global rate limiter shared across ALL scoped instances — keeps total concurrent
    // Spotify calls to 2 and enforces a 300 ms gap between releases to avoid burst 429s.
    private static readonly SemaphoreSlim _globalSlot = new(2, 2);

    public SpotifyService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<SpotifyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    // Resolve credentials from ASP.NET Core config (Spotify__ClientId / Spotify__ClientSecret)
    // OR from plain env vars (SPOTIFY_CLIENT_ID / SPOTIFY_CLIENT_SECRET) for Render compatibility.
    private string? ClientId =>
        NullIfPlaceholder(_config["Spotify:ClientId"])
        ?? NullIfPlaceholder(Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID"));

    private string? ClientSecret =>
        NullIfPlaceholder(_config["Spotify:ClientSecret"])
        ?? NullIfPlaceholder(Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET"));

    private static string? NullIfPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.StartsWith("YOUR_") ? null : value;

    public bool IsConfigured => ClientId != null && ClientSecret != null;

    private async Task<string?> GetAccessToken()
    {
        if (_cache.TryGetValue<string>(TokenCacheKey, out var cached))
            return cached;

        var clientId = ClientId!;
        var clientSecret = ClientSecret!;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        try
        {
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Spotify token request failed: {Status} — {Body}", response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
            _cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain Spotify access token");
            return null;
        }
    }

    private async Task<JsonDocument?> GetAsync(string url)
    {
        var token = await GetAccessToken();
        if (token == null) return null;

        await _globalSlot.WaitAsync();
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Exponential backoff: retry up to 3 times on 429
            int[] backoffSeconds = [5, 15, 30];
            HttpResponseMessage response = null!;
            for (int attempt = 0; attempt <= backoffSeconds.Length; attempt++)
            {
                response = await client.GetAsync(url);
                if ((int)response.StatusCode != 429)
                    break;

                if (attempt == backoffSeconds.Length)
                {
                    _logger.LogError("Spotify 429 persisted after all retries for {Url}", url);
                    break;
                }

                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(backoffSeconds[attempt]);
                // Always wait at least the backoff amount
                var wait = TimeSpan.FromSeconds(Math.Max(retryAfter.TotalSeconds, backoffSeconds[attempt]));
                _logger.LogWarning("Spotify 429 (attempt {Attempt}) — waiting {Seconds}s before retry", attempt + 1, wait.TotalSeconds);
                await Task.Delay(wait);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Spotify API error {Status} for {Url}: {Body}", response.StatusCode, url, body);
                return null;
            }

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spotify request failed: {Url}", url);
            return null;
        }
        finally
        {
            // Gap between releases so concurrent callers don't all burst at once
            await Task.Delay(500);
            _globalSlot.Release();
        }
    }

    public async Task<SpotifyArtistData?> FindArtist(string name)
    {
        var cacheKey = $"spotify:artist:{name.ToLower()}";
        if (_cache.TryGetValue<SpotifyArtistData?>(cacheKey, out var cached))
            return cached;

        var url = $"{ApiBase}/search?q={Uri.EscapeDataString(name)}&type=artist&limit=1";
        using var doc = await GetAsync(url);
        if (doc == null) return null;

        var items = doc.RootElement.GetProperty("artists").GetProperty("items");
        var result = items.GetArrayLength() == 0 ? null : ParseArtist(items[0]);

        _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
        return result;
    }

    public async Task<List<SpotifyArtistData>> SearchArtists(string query, int limit = 10)
    {
        var url = $"{ApiBase}/search?q={Uri.EscapeDataString(query)}&type=artist&limit={limit}";
        using var doc = await GetAsync(url);
        if (doc == null) return new();

        var items = doc.RootElement.GetProperty("artists").GetProperty("items");
        return items.EnumerateArray()
            .Where(el => el.TryGetProperty("name", out _))
            .Select(ParseArtist)
            .ToList();
    }

    // Note: top-tracks and related-artists require OAuth user tokens (not client credentials).
    // We use artist name search to get tracks instead.
    public async Task<List<string>> GetTopTracks(string artistName)
    {
        var url = $"{ApiBase}/search?q={Uri.EscapeDataString($"artist:\"{artistName}\"")}&type=track&limit=10";
        using var doc = await GetAsync(url);
        if (doc == null) return new();

        return doc.RootElement.GetProperty("tracks").GetProperty("items")
            .EnumerateArray()
            .Take(5)
            .Select(t => t.GetProperty("name").GetString()!)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    public async Task<List<SongResult>> SearchTracks(string query, int limit = 10, int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 10); // Spotify Client Credentials caps at 10
        var cacheKey = $"spotify:tracks:{query.ToLower()}:{limit}:{offset}";
        if (_cache.TryGetValue<List<SongResult>>(cacheKey, out var cachedTracks))
            return cachedTracks!;

        var url = $"{ApiBase}/search?q={Uri.EscapeDataString(query)}&type=track&limit={limit}&offset={offset}";
        _logger.LogInformation("SearchTracks: calling {Url}", url);
        using var doc = await GetAsync(url);
        if (doc == null)
        {
            _logger.LogWarning("SearchTracks: GetAsync returned null for query '{Query}'", query);
            return new();
        }

        var results = new List<SongResult>();
        try
        {
            var tracks = doc.RootElement.GetProperty("tracks").GetProperty("items");
            foreach (var track in tracks.EnumerateArray())
            {
                try
                {
                    if (!track.TryGetProperty("artists", out var artistsEl) || artistsEl.GetArrayLength() == 0)
                        continue;

                    var firstArtist = artistsEl[0];
                    var artistName = firstArtist.GetProperty("name").GetString() ?? "Unknown Artist";
                    var artistSpotifyId = firstArtist.TryGetProperty("id", out var aid) ? aid.GetString() : null;
                    var songName = track.TryGetProperty("name", out var sn) ? sn.GetString() ?? "Unknown Song" : "Unknown Song";
                    
                    string? imageUrl = null;
                    if (track.TryGetProperty("album", out var album) && album.TryGetProperty("images", out var images))
                    {
                        if (images.GetArrayLength() > 0)
                            imageUrl = images[images.GetArrayLength() - 1].GetProperty("url").GetString();
                    }

                    string? previewUrl = null;
                    if (track.TryGetProperty("preview_url", out var pv) && pv.ValueKind != JsonValueKind.Null)
                        previewUrl = pv.GetString();

                    string? spotifyTrackId = null;
                    if (track.TryGetProperty("id", out var tid) && tid.ValueKind != JsonValueKind.Null)
                        spotifyTrackId = tid.GetString();

                    string? externalUrl = null;
                    if (track.TryGetProperty("external_urls", out var exUrls) && exUrls.TryGetProperty("spotify", out var sUrl))
                        externalUrl = sUrl.GetString();

                    results.Add(new SongResult
                    {
                        ArtistId = 0, // resolved later by controller
                        ArtistName = artistName,
                        ArtistSpotifyId = artistSpotifyId,
                        SpotifyTrackId = spotifyTrackId,
                        SongName = songName,
                        ImageUrl = imageUrl,
                        PreviewUrl = previewUrl,
                        ExternalUrl = externalUrl,
                        Source = "Spotify"
                    });
                }
                catch (Exception itemEx)
                {
                    _logger.LogWarning(itemEx, "Error parsing individual Spotify track item");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Spotify tracks response");
        }

        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));
        return results;
    }

    // related-artists requires OAuth user tokens — not available with client credentials.
    public Task<List<SpotifyArtistData>> GetRelatedArtists(string spotifyId) =>
        Task.FromResult(new List<SpotifyArtistData>());

    public async Task EnrichArtist(Artist artist)
    {
        var data = await FindArtist(artist.Name);
        if (data == null) return;

        artist.SpotifyId = data.SpotifyId;
        artist.Popularity = data.Popularity;

        if (!string.IsNullOrEmpty(data.ImageUrl))
            artist.ImageUrl = data.ImageUrl;

        if (data.Genres.Any())
            artist.Genres = data.Genres;

        // GetTopTracks makes a second API call — skipped here to keep call count low.
        // Tracks are populated on-demand via SearchTracks when the user searches.
    }

    public async Task<List<string>> GetArtistGenres(string spotifyId)
    {
        var url = $"{ApiBase}/artists/{spotifyId}";
        using var doc = await GetAsync(url);
        if (doc == null) return new();

        if (!doc.RootElement.TryGetProperty("genres", out var genresEl))
            return new();

        return genresEl.EnumerateArray()
            .Select(g => g.GetString()!)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .ToList();
    }

    public static List<string> DeriveVibes(List<string> genres)
    {
        var vibes = new HashSet<string>();
        foreach (var g in genres.Select(g => g.ToLower()))
        {
            if (g.Contains("techno") || g.Contains("industrial") || g.Contains("dark"))
                vibes.Add("Dark");
            if (g.Contains("house") || g.Contains("garage") || g.Contains("funk"))
                vibes.Add("Groovy");
            if (g.Contains("bass") || g.Contains("dubstep") || g.Contains("riddim") || g.Contains("trap"))
                vibes.Add("Bass Heavy");
            if (g.Contains("trance") || g.Contains("progressive") || g.Contains("euphoric"))
                vibes.Add("Euphoric");
            if (g.Contains("drum and bass") || g.Contains("dnb") || g.Contains("jungle") || g.Contains("breakbeat"))
                vibes.Add("Energetic");
            if (g.Contains("ambient") || g.Contains("chill") || g.Contains("downtempo"))
                vibes.Add("Chill");
            if (g.Contains("psytrance") || g.Contains("psy") || g.Contains("psychedelic"))
                vibes.Add("Trippy");
            if (g.Contains("edm") || g.Contains("electro") || g.Contains("big room") || g.Contains("festival"))
                vibes.Add("Festival");
        }
        return vibes.ToList();
    }

    private static SpotifyArtistData ParseArtist(JsonElement el)
    {
        string? imageUrl = null;
        if (el.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
            imageUrl = images[0].TryGetProperty("url", out var u) ? u.GetString() : null;

        var genres = new List<string>();
        if (el.TryGetProperty("genres", out var genresEl))
            genres = genresEl.EnumerateArray().Select(g => g.GetString()!).ToList();

        return new SpotifyArtistData
        {
            SpotifyId = el.GetProperty("id").GetString()!,
            Name = el.GetProperty("name").GetString()!,
            Popularity = el.TryGetProperty("popularity", out var pop) ? pop.GetInt32() : 0,
            Genres = genres,
            ImageUrl = imageUrl
        };
    }
}

public class SpotifyArtistData
{
    public required string SpotifyId { get; set; }
    public required string Name { get; set; }
    public int Popularity { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? ImageUrl { get; set; }
}
