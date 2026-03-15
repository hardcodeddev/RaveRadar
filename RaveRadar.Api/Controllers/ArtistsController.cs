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

            if (_spotifyService.IsConfigured)
            {
                // When searching, augment with live Spotify artist search so nothing is missing
                if (!string.IsNullOrEmpty(search))
                {
                    var spotifyArtists = await _spotifyService.SearchArtists(search, 10);

                    // Load existing DB artists by spotifyId or name to avoid duplicates
                    var spotifyIds = spotifyArtists.Where(s => s.SpotifyId != null).Select(s => s.SpotifyId!).ToList();
                    var spotifyNames = spotifyArtists.Select(s => s.Name.ToLower()).ToList();
                    var existing = await _context.Artists
                        .Where(a => (a.SpotifyId != null && spotifyIds.Contains(a.SpotifyId)) ||
                                    spotifyNames.Contains(a.Name.ToLower()))
                        .ToListAsync();

                    var existingBySpotifyId = existing.Where(a => a.SpotifyId != null)
                        .ToDictionary(a => a.SpotifyId!);
                    var existingByName = existing.ToDictionary(a => a.Name.ToLower());

                    var newArtists = new List<Artist>();
                    foreach (var s in spotifyArtists)
                    {
                        Artist? match = null;
                        if (s.SpotifyId != null) existingBySpotifyId.TryGetValue(s.SpotifyId, out match);
                        match ??= existingByName.GetValueOrDefault(s.Name.ToLower());

                        if (match != null)
                        {
                            // Update image/spotifyId if missing
                            if (string.IsNullOrEmpty(match.ImageUrl) && s.ImageUrl != null)
                                match.ImageUrl = s.ImageUrl;
                            if (match.SpotifyId == null && s.SpotifyId != null)
                                match.SpotifyId = s.SpotifyId;
                        }
                        else
                        {
                            // New artist — insert into DB so it gets a real ID
                            var artist = new Artist
                            {
                                Name = s.Name,
                                SpotifyId = s.SpotifyId,
                                ImageUrl = s.ImageUrl,
                                Popularity = s.Popularity,
                                Genres = s.Genres,
                            };
                            _context.Artists.Add(artist);
                            newArtists.Add(artist);
                        }
                    }

                    if (newArtists.Count > 0 || existing.Any(a => _context.Entry(a).State == Microsoft.EntityFrameworkCore.EntityState.Modified))
                        await _context.SaveChangesAsync();

                    // Merge: DB results first, then newly inserted Spotify artists not already present
                    var resultIds = results.Select(a => a.Id).ToHashSet();
                    results = results
                        .Concat(newArtists.Where(a => !resultIds.Contains(a.Id)))
                        .ToList();
                }
                else
                {
                    // No search term — enrich up to 20 artists missing images
                    var missing = results.Where(a => string.IsNullOrEmpty(a.ImageUrl)).Take(20).ToList();
                    if (missing.Count > 0)
                    {
                        var sem = new SemaphoreSlim(5, 5);
                        await Task.WhenAll(missing.Select(async artist =>
                        {
                            await sem.WaitAsync();
                            try
                            {
                                var data = await _spotifyService.FindArtist(artist.Name);
                                if (data?.ImageUrl != null)
                                {
                                    artist.ImageUrl = data.ImageUrl;
                                    if (artist.SpotifyId == null) artist.SpotifyId = data.SpotifyId;
                                }
                            }
                            finally { sem.Release(); }
                        }));
                        if (missing.Any(a => !string.IsNullOrEmpty(a.ImageUrl)))
                            await _context.SaveChangesAsync();
                    }
                }
            }

            Console.WriteLine($"GetArtists search='{search}' genre='{genre}' returning {results.Count} results.");
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetArtists Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
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
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
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
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpGet("{id:int}")]
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

            // --- Live Spotify search ---
            if (_spotifyService.IsConfigured)
            {
                var tasks = new[]
                {
                    _spotifyService.SearchTracks(q, 10, 0),
                    _spotifyService.SearchTracks($"artist:{q}", 10, 0),
                };
                var batches = await Task.WhenAll(tasks);

                var spotifyResults = batches
                    .SelectMany(b => b)
                    .GroupBy(r => r.SpotifyTrackId ?? $"{r.ArtistName}|{r.SongName}")
                    .Select(g => g.First())
                    .ToList();

                if (spotifyResults.Count > 0)
                {
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
                        Console.WriteLine($"⚠️ DB lookup failed in SearchSongs: {dbEx.Message}");
                    }

                    return spotifyResults;
                }

                Console.WriteLine($"⚠️ Spotify returned no results for '{q}', falling back to local DB.");
            }

            // --- Fallback: DB top-tracks search (also used when Spotify is unconfigured or returns nothing) ---
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

            // If still nothing, return matching artist names as "Artist - Top Tracks" suggestions
            if (results.Count == 0)
            {
                var matchedArtists = artists
                    .Where(a => a.Name.ToLower().Contains(searchLower))
                    .Take(10);

                foreach (var artist in matchedArtists)
                {
                    results.Add(new SongResult
                    {
                        ArtistId = artist.Id,
                        ArtistName = artist.Name,
                        SongName = "Top Tracks",
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
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}
