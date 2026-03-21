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
            Username = "diagnostic",
            Email = "diagnostic@test.com",
            PasswordHash = "",
            SavedTracks = new List<Models.SavedTrack>
            {
                new() { ArtistName = "Deadmau5", SongName = "Strobe", Genres = "progressive house", Vibes = "dark,hypnotic" }
            },
            FavoriteArtists = new List<Models.Artist>
            {
                new() { Id = 0, Name = "Deadmau5", Genres = "progressive house" }
            }
        };

        var dummyCandidates = new List<Models.Artist>
        {
            new() { Id = 1, Name = "Eric Prydz", Genres = "progressive house", Popularity = 80 },
            new() { Id = 2, Name = "Feed Me",    Genres = "electro house",     Popularity = 60 },
        };

        var result = await _mlEngine.GetRecommendations(dummyUser, dummyCandidates);

        if (result == null)
            return StatusCode(503, new { ml_reachable = false, message = "ML engine returned null — check Render logs for '⚠️ RecommendationEngine unavailable'" });

        return Ok(new { ml_reachable = true, source = result.Source, artists = result.Artists });
    }
}
