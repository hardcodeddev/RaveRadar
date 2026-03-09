using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;
using RaveRadar.Api.Services;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SpotifyService _spotifyService;

    public ArtistsController(AppDbContext context, SpotifyService spotifyService)
    {
        _context = context;
        _spotifyService = spotifyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Artist>>> GetArtists([FromQuery] string? search, [FromQuery] string? genre)
    {
        try
        {
            var query = _context.Artists.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Name.ToLower().Contains(search.ToLower()));

            if (!string.IsNullOrEmpty(genre))
            {
                var g = genre.ToLower();
                query = query.Where(a => a.Genres.Any(x => x.ToLower() == g));
            }

            var results = await query.ToListAsync();
            Console.WriteLine($"🔍 GetArtists search='{search}' genre='{genre}' found {results.Count} results.");
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetArtists Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("top")]
    public async Task<ActionResult<IEnumerable<Artist>>> GetTopArtists([FromQuery] int count = 5)
    {
        try
        {
            return await _context.Artists
                .OrderByDescending(a => a.Popularity)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetTopArtists Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("genre/{genre}")]
    public async Task<ActionResult<IEnumerable<Artist>>> GetArtistsByGenre(string genre)
    {
        try
        {
            var g = genre.ToLower();
            return await _context.Artists
                .Where(a => a.Genres.Any(x => x.ToLower() == g))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetArtistsByGenre Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Artist>> GetArtist(int id)
    {
        var artist = await _context.Artists.FindAsync(id);
        if (artist == null) return NotFound();
        return artist;
    }

    [HttpGet("songs/search")]
    public async Task<ActionResult<IEnumerable<SongResult>>> SearchSongs([FromQuery] string? q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return new List<SongResult>();

            // --- Live Spotify search: 3 parallel queries for breadth ---
            if (_spotifyService.IsConfigured)
            {
                var tasks = new[]
                {
                    _spotifyService.SearchTracks(q, 10, 0),
                    _spotifyService.SearchTracks(q, 10, 10),
                    _spotifyService.SearchTracks($"artist:{q}", 10, 0),
                };
                var batches = await Task.WhenAll(tasks);

                var spotifyResults = batches
                    .SelectMany(b => b)
                    .GroupBy(r => r.SpotifyTrackId ?? $"{r.ArtistName}|{r.SongName}")
                    .Select(g => g.First())
                    .ToList();

                // Resolve local artist IDs where possible
                try
                {
                    var names = spotifyResults.Select(r => r.ArtistName.ToLower()).Distinct().ToList();
                    var localByName = await _context.Artists
                        .Where(a => names.Contains(a.Name.ToLower()))
                        .ToListAsync();
                    
                    var localDict = localByName
                        .GroupBy(a => a.Name.ToLower())
                        .ToDictionary(g => g.Key, g => g.First().Id);

                    foreach (var result in spotifyResults)
                    {
                        if (localDict.TryGetValue(result.ArtistName.ToLower(), out var localId))
                            result.ArtistId = localId;
                    }
                }
                catch (Exception dbEx)
                {
                    // Log but don't fail the whole request if DB lookup fails
                    Console.WriteLine($"⚠️ DB lookup failed in SearchSongs: {dbEx.Message}");
                }

                return spotifyResults;
            }

            // --- Fallback: DB top-tracks search ---
            var searchLower = q.ToLower();
            var artists = await _context.Artists.ToListAsync();
            var placeholders = new HashSet<string> { "Track 1", "Track 2", "Track 3" };
            var results = new List<SongResult>();

            foreach (var artist in artists)
            {
                foreach (var track in artist.TopTracks
                    .Where(t => !placeholders.Contains(t))
                    .Where(t => t.ToLower().Contains(searchLower) || artist.Name.ToLower().Contains(searchLower)))
                {
                    results.Add(new SongResult
                    {
                        ArtistId = artist.Id,
                        ArtistName = artist.Name,
                        SongName = track,
                        ImageUrl = artist.ImageUrl,
                        Source = "local"
                    });
                }
            }

            return results.Take(20).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SearchSongs Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
