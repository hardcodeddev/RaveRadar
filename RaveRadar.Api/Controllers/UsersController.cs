using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;
using RaveRadar.Api.Services;
using BCrypt.Net;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SpotifyService _spotifyService;
    private readonly IMemoryCache _cache;
    private readonly RecommendationEngineService _mlEngine;

    public UsersController(AppDbContext context, SpotifyService spotifyService, IMemoryCache cache, RecommendationEngineService mlEngine)
    {
        _context = context;
        _spotifyService = spotifyService;
        _cache = cache;
        _mlEngine = mlEngine;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(int userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.FavoriteArtists)
                .Include(u => u.FavoriteGenres)
                .Include(u => u.SavedTracks)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();
            return Ok(MapUser(user));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        try
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("User already exists");

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Location = dto.Location
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"✅ User registered: {user.Email}");
            return Ok(new { user.Id, user.Email, user.Location });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Register Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.FavoriteArtists)
                .Include(u => u.FavoriteGenres)
                .Include(u => u.SavedTracks)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials");

            Console.WriteLine($"✅ User logged in: {user.Email}");
            return Ok(MapUser(user));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Login Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpPost("{userId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(int userId, PreferencesDto dto)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.FavoriteArtists)
                .Include(u => u.FavoriteGenres)
                .Include(u => u.SavedTracks)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            user.Location = dto.Location;

            if (dto.ArtistIds != null)
            {
                user.FavoriteArtists.Clear();
                var newArtists = await _context.Artists
                    .Where(a => dto.ArtistIds.Contains(a.Id))
                    .Take(10)
                    .ToListAsync();
                foreach (var a in newArtists) user.FavoriteArtists.Add(a);
            }

            if (dto.GenreIds != null)
            {
                user.FavoriteGenres.Clear();
                var newGenres = await _context.Genres
                    .Where(g => dto.GenreIds.Contains(g.Id))
                    .Take(10)
                    .ToListAsync();
                foreach (var g in newGenres) user.FavoriteGenres.Add(g);
            }

            if (dto.FavoriteSongs != null)
                user.FavoriteSongs = dto.FavoriteSongs.Take(20).ToList();

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Preferences updated for user {userId}");
            return Ok(MapUser(user));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpdatePreferences Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpPost("{userId}/favorites/songs")]
    public async Task<IActionResult> ToggleFavoriteSong(int userId, [FromBody] string songName)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (user.FavoriteSongs.Contains(songName))
            user.FavoriteSongs.Remove(songName);
        else
            user.FavoriteSongs.Add(songName);

        await _context.SaveChangesAsync();
        return Ok(user.FavoriteSongs);
    }

    [HttpPost("{userId}/favorites/artists/{artistId}")]
    public async Task<IActionResult> AddFavoriteArtist(int userId, int artistId)
    {
        var user = await _context.Users
            .Include(u => u.FavoriteArtists)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound("User not found");

        var artist = await _context.Artists.FindAsync(artistId);
        if (artist == null) return NotFound("Artist not found");

        if (!user.FavoriteArtists.Any(a => a.Id == artistId))
            user.FavoriteArtists.Add(artist);

        await _context.SaveChangesAsync();
        return Ok(user.FavoriteArtists.Select(a => new { a.Id, a.Name, a.ImageUrl, a.Genres, a.Popularity }));
    }

    [HttpDelete("{userId}/favorites/artists/{artistId}")]
    public async Task<IActionResult> RemoveFavoriteArtist(int userId, int artistId)
    {
        var user = await _context.Users
            .Include(u => u.FavoriteArtists)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound("User not found");

        var artist = user.FavoriteArtists.FirstOrDefault(a => a.Id == artistId);
        if (artist == null) return NotFound("Artist not in favorites");

        user.FavoriteArtists.Remove(artist);
        await _context.SaveChangesAsync();
        return Ok(user.FavoriteArtists.Select(a => new { a.Id, a.Name, a.ImageUrl, a.Genres, a.Popularity }));
    }

    [HttpPost("{userId}/saved-tracks")]
    public async Task<IActionResult> SaveTrack(int userId, SaveTrackDto dto)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.SavedTracks)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // Avoid duplicates by spotifyTrackId or artist+song combo
            var exists = user.SavedTracks.Any(t =>
                (!string.IsNullOrEmpty(dto.SpotifyTrackId) && t.SpotifyTrackId == dto.SpotifyTrackId)
                || (t.ArtistName == dto.ArtistName && t.SongName == dto.SongName));

            if (exists)
                return Ok(user.SavedTracks);

            // Enrich with genres + vibes from Spotify
            var genres = new List<string>();
            if (_spotifyService.IsConfigured && !string.IsNullOrEmpty(dto.ArtistSpotifyId))
                genres = await _spotifyService.GetArtistGenres(dto.ArtistSpotifyId);

            var vibes = SpotifyService.DeriveVibes(genres);

            var track = new SavedTrack
            {
                SpotifyTrackId = dto.SpotifyTrackId,
                SongName = dto.SongName,
                ArtistName = dto.ArtistName,
                ArtistSpotifyId = dto.ArtistSpotifyId,
                ImageUrl = dto.ImageUrl,
                PreviewUrl = dto.PreviewUrl,
                ExternalUrl = dto.ExternalUrl,
                Genres = genres,
                Vibes = vibes,
                UserId = userId
            };

            user.SavedTracks.Add(track);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Track saved for user {userId}: {dto.SongName}");

            // Fire-and-forget audio enrichment — does not block the response
            var trackId = track.Id;
            var artistName = dto.ArtistName;
            var songName = dto.SongName;
            _ = Task.Run(async () =>
            {
                try
                {
                    var features = await _mlEngine.GetTrackFeatures(artistName, songName);
                    if (features != null)
                    {
                        var saved = await _context.SavedTracks.FindAsync(trackId);
                        if (saved != null)
                        {
                            saved.BpmValue = features.BpmValue;
                            saved.EnergyScore = features.EnergyScore;
                            saved.DanceabilityScore = features.DanceabilityScore;
                            saved.ValenceScore = features.ValenceScore;
                            saved.DarknessScore = features.DarknessScore;
                            saved.AudioFeaturesEnriched = true;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Audio enrichment failed for track {trackId}: {ex.Message}");
                }
            });

            return Ok(user.SavedTracks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SaveTrack Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpDelete("{userId}/saved-tracks/{trackId}")]
    public async Task<IActionResult> RemoveSavedTrack(int userId, int trackId)
    {
        var user = await _context.Users
            .Include(u => u.SavedTracks)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        var track = user.SavedTracks.FirstOrDefault(t => t.Id == trackId);
        if (track == null) return NotFound();

        user.SavedTracks.Remove(track);
        await _context.SaveChangesAsync();

        return Ok(user.SavedTracks);
    }

    [HttpGet("{userId}/recommendations")]
    public async Task<IActionResult> GetRecommendations(int userId)
    {
        // Cache per user for 30 min — prevents re-firing 16 Spotify calls on every page visit
        var cacheKey = $"recs:{userId}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return Ok(cached);

        try
        {
            var user = await _context.Users
                .Include(u => u.FavoriteArtists)
                .Include(u => u.FavoriteGenres)
                .Include(u => u.SavedTracks)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            var favoriteIds = user.FavoriteArtists.Select(a => a.Id).ToHashSet();

            var allPool = await _context.Artists
                .Where(a => !favoriteIds.Contains(a.Id))
                .ToListAsync();

            // --- ML engine (primary) ---
            var mlResult = await _mlEngine.GetRecommendations(user, allPool);
            if (mlResult != null && mlResult.Artists.Count > 0)
            {
                var artistById = allPool.ToDictionary(a => a.Id);
                var mlArtists = mlResult.Artists
                    .Where(s => artistById.ContainsKey(s.Id))
                    .Select(s => new { Artist = artistById[s.Id], Reason = s.Reason })
                    .ToList();

                var mlSongSources = mlArtists.Take(6)
                    .Select(r => (r.Artist, r.Reason, IsFav: false))
                    .Concat(user.FavoriteArtists.Select(a => (a, $"By your favorite, {a.Name}", IsFav: true)))
                    .DistinctBy(r => r.Item1.Id)
                    .ToList();

                List<object> mlSongs;
                if (_spotifyService.IsConfigured && mlSongSources.Any())
                {
                    var batches = new List<(List<SongResult> Results, string Name, string Reason)>();
                    foreach (var r in mlSongSources.Take(4))
                    {
                        var results = await _spotifyService.SearchTracks($"artist:\"{r.Item1.Name}\"", 10, 0);
                        batches.Add((results, r.Item1.Name, r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2));
                    }
                    var mlArtistNames = mlSongSources.Select(r => r.Item1.Name.ToLower()).Distinct().ToList();
                    var mlLocalByName = await _context.Artists
                        .Where(a => mlArtistNames.Contains(a.Name.ToLower()))
                        .ToDictionaryAsync(a => a.Name.ToLower());

                    mlSongs = batches
                        .SelectMany(b => b.Results.Select(track =>
                        {
                            mlLocalByName.TryGetValue(track.ArtistName.ToLower(), out var local);
                            return (object)new
                            {
                                ArtistId = local?.Id ?? 0,
                                track.ArtistName,
                                track.SongName,
                                track.ArtistSpotifyId,
                                track.SpotifyTrackId,
                                track.ImageUrl,
                                track.PreviewUrl,
                                track.ExternalUrl,
                                Source = "Spotify",
                                b.Reason
                            };
                        }))
                        .Cast<dynamic>()
                        .GroupBy(s => (string)s.SpotifyTrackId ?? $"{(string)s.ArtistName}|{(string)s.SongName}")
                        .Select(g => g.First())
                        .Take(40)
                        .Cast<object>()
                        .ToList();
                }
                else
                {
                    var placeholders = new HashSet<string> { "Track 1", "Track 2", "Track 3" };
                    mlSongs = mlSongSources
                        .SelectMany(r => r.Item1.TopTracks
                            .Where(t => !placeholders.Contains(t))
                            .Select(t => (object)new
                            {
                                ArtistId = r.Item1.Id,
                                ArtistName = r.Item1.Name,
                                SongName = t,
                                ArtistSpotifyId = (string?)null,
                                SpotifyTrackId = (string?)null,
                                ImageUrl = r.Item1.ImageUrl,
                                PreviewUrl = (string?)null,
                                ExternalUrl = (string?)null,
                                Source = "local",
                                Reason = r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2
                            }))
                        .GroupBy(s => ((dynamic)s).SongName)
                        .Select(g => g.First())
                        .Take(24)
                        .ToList();
                }

                Console.WriteLine($"✅ ML engine: {mlArtists.Count} artist recs for user {userId}");
                var mlResponse = new
                {
                    Artists = mlArtists.Select(r => new
                    {
                        r.Artist.Id, r.Artist.Name, r.Artist.ImageUrl,
                        Genres = r.Artist.Genres,
                        r.Artist.Popularity, r.Artist.Bio,
                        TopTracks = r.Artist.TopTracks,
                        Reason = r.Reason,
                        FromMl = true
                    }),
                    Songs = mlSongs
                };
                _cache.Set(cacheKey, mlResponse, TimeSpan.FromMinutes(30));
                return Ok(mlResponse);
            }

            var recommendedArtists = new List<(Artist Artist, string Reason)>();

            // --- Genre matching (fallback) ---
            var targetGenres = user.FavoriteArtists
                .SelectMany(a => a.Genres)
                .Concat(user.FavoriteGenres.Select(g => g.Name))
                .Select(g => g.ToLower())
                .Distinct()
                .ToList();

            if (targetGenres.Any())
            {
                foreach (var artist in allPool.OrderByDescending(a => a.Popularity).Take(12))
                {
                    var matchedGenre = artist.Genres
                        .FirstOrDefault(g => targetGenres.Contains(g.ToLower()));
                    if (matchedGenre != null)
                    {
                        var seedForGenre = user.FavoriteArtists
                            .FirstOrDefault(fa => fa.Genres.Any(g => g.ToLower() == matchedGenre.ToLower()));
                        var reason = seedForGenre != null
                            ? $"Fans of {seedForGenre.Name} also like this"
                            : $"Matches your {matchedGenre} taste";
                        recommendedArtists.Add((artist, reason));
                        if (recommendedArtists.Count >= 12) break;
                    }
                }
            }

            // --- Pad with trending if not enough ---
            if (recommendedArtists.Count < 4)
            {
                var excludeIds = recommendedArtists.Select(r => r.Artist.Id).Concat(favoriteIds).ToHashSet();
                foreach (var artist in allPool.Where(a => !excludeIds.Contains(a.Id)).OrderByDescending(a => a.Popularity))
                {
                    recommendedArtists.Add((artist, "Trending in EDM"));
                    if (recommendedArtists.Count >= 12) break;
                }
            }

            // --- Song recommendations: live Spotify searches per artist ---
            var songSourceArtists = recommendedArtists.Take(6)
                .Select(r => (r.Artist, r.Reason, IsFav: false))
                .Concat(user.FavoriteArtists.Select(a => (a, $"By your favorite, {a.Name}", IsFav: true)))
                .DistinctBy(r => r.Item1.Id)
                .ToList();

            List<object> songs;
            if (_spotifyService.IsConfigured && songSourceArtists.Any())
            {
                // Sequential to avoid bursting Spotify — results are cached 10min per artist so
                // repeat calls are free. Take(4) keeps total calls reasonable per recommendation load.
                var batches = new List<(List<SongResult> Results, string Name, string Reason)>();
                foreach (var r in songSourceArtists.Take(4))
                {
                    var results = await _spotifyService.SearchTracks($"artist:\"{r.Item1.Name}\"", 10, 0);
                    batches.Add((results, r.Item1.Name, r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2));
                }

                // Resolve local artist IDs
                var allArtistNames = songSourceArtists.Select(r => r.Item1.Name.ToLower()).Distinct().ToList();
                var localByName = await _context.Artists
                    .Where(a => allArtistNames.Contains(a.Name.ToLower()))
                    .ToDictionaryAsync(a => a.Name.ToLower());

                songs = batches
                    .SelectMany(b => b.Results.Select(track =>
                    {
                        localByName.TryGetValue(track.ArtistName.ToLower(), out var local);
                        return (object)new
                        {
                            ArtistId = local?.Id ?? 0,
                            track.ArtistName,
                            track.SongName,
                            track.ArtistSpotifyId,
                            track.SpotifyTrackId,
                            track.ImageUrl,
                            track.PreviewUrl,
                            track.ExternalUrl,
                            Source = "Spotify",
                            b.Reason
                        };
                    }))
                    .Cast<dynamic>()
                    .GroupBy(s => (string)s.SpotifyTrackId ?? $"{(string)s.ArtistName}|{(string)s.SongName}")
                    .Select(g => g.First())
                    .Take(40)
                    .Cast<object>()
                    .ToList();
            }
            else
            {
                // Fallback to local TopTracks
                var placeholders = new HashSet<string> { "Track 1", "Track 2", "Track 3" };
                songs = songSourceArtists
                    .SelectMany(r => r.Item1.TopTracks
                        .Where(t => !placeholders.Contains(t))
                        .Select(t => (object)new
                        {
                            ArtistId = r.Item1.Id,
                            ArtistName = r.Item1.Name,
                            SongName = t,
                            ArtistSpotifyId = (string?)null,
                            SpotifyTrackId = (string?)null,
                            ImageUrl = r.Item1.ImageUrl,
                            PreviewUrl = (string?)null,
                            ExternalUrl = (string?)null,
                            Source = "local",
                            Reason = r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2
                        }))
                    .GroupBy(s => ((dynamic)s).SongName)
                    .Select(g => g.First())
                    .Take(24)
                    .ToList();
            }

            Console.WriteLine($"✅ Generated {recommendedArtists.Count} artist recs and {songs.Count} song recs for user {userId}");
            var result = new
            {
                Artists = recommendedArtists.Select(r => new
                {
                    r.Artist.Id, r.Artist.Name, r.Artist.ImageUrl,
                    Genres = r.Artist.Genres,
                    r.Artist.Popularity, r.Artist.Bio,
                    TopTracks = r.Artist.TopTracks,
                    Reason = r.Reason,
                    FromMl = false
                }),
                Songs = songs
            };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetRecommendations Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    private static object MapUser(User user) => new
    {
        user.Id,
        user.Email,
        user.Location,
        FavoriteArtists = user.FavoriteArtists.Select(a => new { a.Id, a.Name, a.ImageUrl, a.Genres, a.Popularity }),
        FavoriteGenres = user.FavoriteGenres.Select(g => new { g.Id, g.Name }),
        user.FavoriteSongs,
        SavedTracks = user.SavedTracks.OrderByDescending(t => t.AddedAt).Select(t => new
        {
            t.Id, t.SpotifyTrackId, t.SongName, t.ArtistName, t.ArtistSpotifyId,
            t.ImageUrl, t.PreviewUrl, t.ExternalUrl, t.Genres, t.Vibes, t.AddedAt,
            t.BpmValue, t.EnergyScore, t.DanceabilityScore, t.ValenceScore, t.DarknessScore,
            t.AudioFeaturesEnriched
        })
    };
}

public class SaveTrackDto
{
    public string? SpotifyTrackId { get; set; }
    public required string SongName { get; set; }
    public required string ArtistName { get; set; }
    public string? ArtistSpotifyId { get; set; }
    public string? ImageUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? ExternalUrl { get; set; }
}

public class RegisterDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? Location { get; set; }
}

public class LoginDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class PreferencesDto
{
    public string? Location { get; set; }
    public List<int>? ArtistIds { get; set; }
    public List<int>? GenreIds { get; set; }
    public List<string>? FavoriteSongs { get; set; }
}
