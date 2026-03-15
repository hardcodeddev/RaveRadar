using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;
using RaveRadar.Api.Services;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SpotifyService _spotifyService;

    public EventsController(AppDbContext context, SpotifyService spotifyService)
    {
        _context = context;
        _spotifyService = spotifyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Event>>> GetEvents(
        [FromQuery] string? city,
        [FromQuery] int? userId,
        [FromQuery] bool allCities = false)
    {
        try
        {
            if (userId.HasValue)
            {
                var user = await _context.Users
                    .Include(u => u.FavoriteArtists)
                    .Include(u => u.FavoriteGenres)
                    .Include(u => u.SavedTracks)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (user != null)
                {
                    var eventsQuery = _context.Events.AsQueryable();
                    var effectiveCity = city ?? user.Location;

                    if (!allCities && !string.IsNullOrEmpty(effectiveCity))
                    {
                        var cityLower = effectiveCity.ToLower();
                        eventsQuery = eventsQuery.Where(e => e.City != null &&
                            (e.City.ToLower() == cityLower ||
                             e.City.ToLower().Contains(cityLower) ||
                             cityLower.Contains(e.City.ToLower())));
                    }

                    var events = await eventsQuery.ToListAsync();

                    // Build genre/vibe profiles for every artist on every event
                    var allArtistNames = events.SelectMany(e => e.ArtistNames).Distinct().ToList();
                    var artistProfiles = await BuildArtistProfilesAsync(allArtistNames);

                    // Score, sort, and attach reasons
                    var scoredEvents = events
                        .Select(e =>
                        {
                            var (score, reason) = ScoreWithReason(e, user, artistProfiles);
                            // Enrich genreNames with what Spotify told us about the artists
                            var enrichedGenres = e.ArtistNames
                                .SelectMany(n => artistProfiles.TryGetValue(n.ToLower(), out var p) && p.Genres.Any()
                                    ? p.Genres
                                    : Enumerable.Empty<string>())
                                .Concat(e.GenreNames)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(6)
                                .ToList();
                            return new { Event = e, Score = score, Reason = reason, EnrichedGenres = enrichedGenres };
                        })
                        .OrderByDescending(s => s.Score)
                        .ThenBy(s => s.Event.Date)
                        .Select(s => new
                        {
                            s.Event.Id,
                            s.Event.Name,
                            s.Event.Date,
                            s.Event.Venue,
                            s.Event.City,
                            s.Event.TicketUrl,
                            s.Event.ImageUrl,
                            s.Event.Latitude,
                            s.Event.Longitude,
                            s.Event.ArtistNames,
                            GenreNames = s.EnrichedGenres,
                            s.Reason
                        })
                        .ToList();

                    return Ok(scoredEvents);
                }
            }

            // Unauthenticated — just filter by city and return
            var query = _context.Events.AsQueryable();
            if (!string.IsNullOrEmpty(city))
            {
                var cityLower = city.ToLower();
                query = query.Where(e => e.City != null &&
                    (e.City.ToLower() == cityLower ||
                     e.City.ToLower().Contains(cityLower) ||
                     cityLower.Contains(e.City.ToLower())));
            }

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetEvents Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpGet("cities")]
    public async Task<ActionResult<IEnumerable<string>>> GetCities([FromQuery] string? search)
    {
        try
        {
            var query = _context.Events
                .Where(e => e.City != null && e.City != "")
                .Select(e => e.City!)
                .Distinct();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c => c.ToLower().Contains(search.ToLower()));

            var results = await query.OrderBy(c => c).Take(20).ToListAsync();
            Console.WriteLine($"🔍 GetCities search='{search}' found {results.Count} results.");
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetCities Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    // ── Artist enrichment ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a genre + vibe profile for each artist name by checking the DB first,
    /// then calling Spotify for any not yet in the DB (capped at 15 new lookups).
    /// New artists discovered here are persisted so future calls are instant.
    /// </summary>
    private async Task<Dictionary<string, EventArtistProfile>> BuildArtistProfilesAsync(List<string> artistNames)
    {
        if (artistNames.Count == 0) return new();

        var nameLowers = artistNames.Select(n => n.ToLower()).Distinct().ToList();

        // Single DB query for all known artists
        var dbArtists = await _context.Artists
            .Where(a => nameLowers.Contains(a.Name.ToLower()))
            .ToListAsync();

        var profiles = new Dictionary<string, EventArtistProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in dbArtists)
        {
            profiles[a.Name.ToLower()] = new EventArtistProfile
            {
                Name = a.Name,
                Genres = a.Genres,
                Vibes = SpotifyService.DeriveVibes(a.Genres)
            };
        }

        // Spotify fallback for artists not in DB (cap at 15 to avoid hammering rate limits)
        if (_spotifyService.IsConfigured)
        {
            var missing = nameLowers.Where(n => !profiles.ContainsKey(n)).Take(8).ToList();
            if (missing.Any())
            {
                var sem = new SemaphoreSlim(2, 2);
                var toInsert = new System.Collections.Concurrent.ConcurrentBag<Artist>();

                await Task.WhenAll(missing.Select(async nameLower =>
                {
                    await sem.WaitAsync();
                    try
                    {
                        var originalName = artistNames.First(n => n.ToLower() == nameLower);
                        var data = await _spotifyService.FindArtist(originalName);
                        if (data != null && (data.Genres.Any() || data.ImageUrl != null))
                        {
                            var vibes = SpotifyService.DeriveVibes(data.Genres);
                            profiles[nameLower] = new EventArtistProfile
                            {
                                Name = originalName,
                                Genres = data.Genres,
                                Vibes = vibes
                            };
                            toInsert.Add(new Artist
                            {
                                Name = originalName,
                                SpotifyId = data.SpotifyId,
                                ImageUrl = data.ImageUrl,
                                Genres = data.Genres,
                                Popularity = data.Popularity
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Spotify lookup failed for artist: {ex.Message}");
                    }
                    finally { sem.Release(); }
                }));

                if (toInsert.Any())
                {
                    // Guard against duplicates from concurrent requests
                    var insertedLowers = toInsert.Select(a => a.Name.ToLower()).ToList();
                    var alreadyExist = await _context.Artists
                        .Where(a => insertedLowers.Contains(a.Name.ToLower()))
                        .Select(a => a.Name.ToLower())
                        .ToListAsync();

                    foreach (var artist in toInsert.Where(a => !alreadyExist.Contains(a.Name.ToLower())))
                        _context.Artists.Add(artist);

                    await _context.SaveChangesAsync();
                }
            }
        }

        return profiles;
    }

    // ── Scoring ──────────────────────────────────────────────────────────────

    private static (int Score, string? Reason) ScoreWithReason(
        Event e,
        User user,
        Dictionary<string, EventArtistProfile> artistProfiles)
    {
        int score = 0;
        var reasonParts = new List<string>();

        var favArtistNames = user.FavoriteArtists.Select(a => a.Name.ToLower()).ToHashSet();
        var favGenreNames  = user.FavoriteGenres.Select(g => g.Name.ToLower()).ToHashSet();

        // Build the user's vibe fingerprint from favourite artists + saved tracks
        var userVibes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in user.FavoriteArtists)
            foreach (var v in SpotifyService.DeriveVibes(a.Genres))
                userVibes.Add(v);
        foreach (var t in user.SavedTracks)
            foreach (var v in t.Vibes)
                userVibes.Add(v);

        // Also derive user genre interests from favourite artists (broader than FavoriteGenres list)
        var userArtistGenres = user.FavoriteArtists
            .SelectMany(a => a.Genres)
            .Select(g => g.ToLower())
            .ToHashSet();

        // ── 1. Exact favourite-artist match ──────────────────────────────────
        var matchedArtists = e.ArtistNames
            .Where(n => favArtistNames.Contains(n.ToLower()))
            .ToList();

        if (matchedArtists.Any())
        {
            score += 80 * matchedArtists.Count;
            var artistLabel = matchedArtists.Count == 1
                ? $"{matchedArtists[0]} is playing"
                : $"{matchedArtists[0]} & {matchedArtists.Count - 1} more favourites are on the lineup";
            reasonParts.Add(artistLabel);
        }

        // ── 2. Collect event-wide genres & vibes from Spotify-enriched profiles ─
        var eventGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventVibes  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artistName in e.ArtistNames)
        {
            if (artistProfiles.TryGetValue(artistName.ToLower(), out var profile))
            {
                foreach (var g in profile.Genres) eventGenres.Add(g);
                foreach (var v in profile.Vibes)  eventVibes.Add(v);
            }
        }
        // Also include whatever EdmTrain stored
        foreach (var g in e.GenreNames) eventGenres.Add(g);

        // ── 3. Saved-genre match ──────────────────────────────────────────────
        var matchedFavGenres = eventGenres
            .Where(g => favGenreNames.Contains(g.ToLower()))
            .ToList();

        if (matchedFavGenres.Any())
        {
            score += 40 * Math.Min(matchedFavGenres.Count, 3);
            var genreLabel = matchedFavGenres.Count == 1
                ? $"{matchedFavGenres[0]} — one of your favourite genres"
                : $"{matchedFavGenres[0]} & {matchedFavGenres[1]} match your taste";
            reasonParts.Add(genreLabel);
        }

        // ── 4. Vibe match (from Spotify genre analysis) ──────────────────────
        var matchedVibes = eventVibes.Where(v => userVibes.Contains(v)).ToList();
        // Only surface vibe reason when it adds new info beyond a genre match
        if (matchedVibes.Any() && matchedFavGenres.Count == 0)
        {
            score += 30 * Math.Min(matchedVibes.Count, 2);

            // Find an artist responsible so the reason is specific
            var vibeArtist = e.ArtistNames.FirstOrDefault(n =>
                artistProfiles.TryGetValue(n.ToLower(), out var p) &&
                p.Vibes.Any(v => matchedVibes.Contains(v)));

            var vibeLabel = matchedVibes.Count == 1 ? matchedVibes[0] : $"{matchedVibes[0]} & {matchedVibes[1]}";
            reasonParts.Add(vibeArtist != null
                ? $"{vibeArtist} brings the {vibeLabel} energy you're into"
                : $"Matches your {vibeLabel} vibe");
        }

        // ── 5. Discovery — artist-genre overlap with user's taste (soft signal) ─
        if (score == 0 && eventGenres.Any())
        {
            var discoveryMatches = eventGenres
                .Where(g => userArtistGenres.Contains(g.ToLower()))
                .Take(2)
                .ToList();

            if (discoveryMatches.Any())
            {
                score += 15;
                var discoverArtist = e.ArtistNames.FirstOrDefault(n =>
                    artistProfiles.TryGetValue(n.ToLower(), out var p) &&
                    p.Genres.Any(g => discoveryMatches.Contains(g, StringComparer.OrdinalIgnoreCase)));
                reasonParts.Add(discoverArtist != null
                    ? $"Discover {discoverArtist} — {discoveryMatches[0]} sound similar to your favourites"
                    : $"Similar {discoveryMatches[0]} sound to artists you follow");
            }
        }

        // ── 6. Location match ─────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(user.Location) && e.City != null &&
            e.City.ToLower().Contains(user.Location.ToLower()))
        {
            score += 60;
            // Append city to existing reason or start one
            if (reasonParts.Count > 0)
                reasonParts.Add($"in {e.City}");
            else
                reasonParts.Add($"Near you in {e.City}");
        }

        string? reason = reasonParts.Count > 0 ? string.Join(" · ", reasonParts) : null;
        return (score, reason);
    }
}

/// <summary>Genre + vibe profile for an event artist, built from DB + Spotify.</summary>
internal sealed class EventArtistProfile
{
    public required string Name { get; init; }
    public List<string> Genres { get; init; } = new();
    public List<string> Vibes  { get; init; } = new();
}
