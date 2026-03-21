using Microsoft.AspNetCore.Mvc;
using RaveRadar.Api.Services;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly RecommendationEngineService _mlEngine;

    public DiagnosticsController(RecommendationEngineService mlEngine)
    {
        _mlEngine = mlEngine;
    }

    /// <summary>
    /// Pings the ML engine with a minimal dummy payload and returns its raw response.
    /// Use this to verify the Python service is reachable from the API container.
    /// GET /api/diagnostics/ml-ping
    /// </summary>
    [HttpGet("ml-ping")]
    public async Task<IActionResult> MlPing()
    {
        var dummyUser = new Models.User
        {
            Id = 0,
            Email = "diagnostic@test.com",
            PasswordHash = "",
            SavedTracks = new List<Models.SavedTrack>
            {
                new() { ArtistName = "Deadmau5",   SongName = "Strobe",             Genres = new List<string> { "progressive house" }, Vibes = new List<string> { "dark", "hypnotic" } },
                new() { ArtistName = "Eric Prydz", SongName = "Pjanoo",             Genres = new List<string> { "progressive house" }, Vibes = new List<string> { "energetic", "euphoric" } },
                new() { ArtistName = "Feed Me",    SongName = "One Click Headshot", Genres = new List<string> { "electro house" },     Vibes = new List<string> { "aggressive", "dark" } },
            },
            FavoriteArtists = new List<Models.Artist>
            {
                new() { Id = 0, Name = "Deadmau5", Genres = new List<string> { "progressive house" } }
            }
        };

        var dummyCandidates = new List<Models.Artist>
        {
            new() { Id = 0, Name = "Deadmau5",   Genres = new List<string> { "progressive house" }, Popularity = 90 },
            new() { Id = 1, Name = "Eric Prydz", Genres = new List<string> { "progressive house" }, Popularity = 80 },
            new() { Id = 2, Name = "Feed Me",    Genres = new List<string> { "electro house" },     Popularity = 60 },
        };

        var result = await _mlEngine.GetRecommendations(dummyUser, dummyCandidates);

        if (result == null)
            return StatusCode(503, new { ml_reachable = false, message = "ML engine returned null — check Render logs for '⚠️ RecommendationEngine unavailable'" });

        return Ok(new { ml_reachable = true, source = result.Source, artists = result.Artists });
    }
}
