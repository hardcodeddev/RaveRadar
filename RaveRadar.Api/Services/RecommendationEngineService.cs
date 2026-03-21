using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaveRadar.Api.Models;

namespace RaveRadar.Api.Services;

public class RecommendEngineResult
{
    [JsonPropertyName("artists")]
    public List<ScoredArtistDto> Artists { get; set; } = new();

    [JsonPropertyName("source")]
    public string Source { get; set; } = "ml";
}

public class ScoredArtistDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public class AudioFeatureDto
{
    [JsonPropertyName("bpm_value")]
    public float? BpmValue { get; set; }

    [JsonPropertyName("energy_score")]
    public float? EnergyScore { get; set; }

    [JsonPropertyName("danceability_score")]
    public float? DanceabilityScore { get; set; }

    [JsonPropertyName("valence_score")]
    public float? ValenceScore { get; set; }

    [JsonPropertyName("darkness_score")]
    public float? DarknessScore { get; set; }
}

public class RecommendationEngineService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public RecommendationEngineService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _http = httpClientFactory.CreateClient("RecommendationEngine");
        _baseUrl = config["RecommendationEngine:Url"] ?? "http://localhost:8000";
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<RecommendEngineResult?> GetRecommendations(
        User user,
        List<Artist> candidateArtists)
    {
        try
        {
            var savedTracks = user.SavedTracks ?? new List<SavedTrack>();
            var payload = new
            {
                user_id = user.Id,
                saved_tracks = savedTracks.Select(t => new
                {
                    artist_name = t.ArtistName,
                    song_name = t.SongName,
                    genres = t.Genres,
                    vibes = t.Vibes,
                    added_at = t.AddedAt.ToString("O"),
                }),
                favorite_artist_names = user.FavoriteArtists.Select(a => a.Name).ToList(),
                candidate_artists = candidateArtists.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    genres = a.Genres,
                    vibes = SpotifyService.DeriveVibes(a.Genres),
                    top_tracks = a.TopTracks,
                    popularity = a.Popularity,
                }),
            };

            var response = await _http.PostAsJsonAsync($"{_baseUrl}/recommend", payload);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<RecommendEngineResult>();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ RecommendationEngine unavailable: {ex.Message}");
            return null;
        }
    }

    public async Task<AudioFeatureDto?> GetTrackFeatures(string artistName, string songName)
    {
        try
        {
            var payload = new { artist_name = artistName, song_name = songName };
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/track-features", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AudioFeatureDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ GetTrackFeatures failed: {ex.Message}");
            return null;
        }
    }
}
