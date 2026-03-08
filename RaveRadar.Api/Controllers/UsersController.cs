using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public UsersController(AppDbContext context, SpotifyService spotifyService)
    {
        _context = context;
        _spotifyService = spotifyService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
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

        return Ok(new { user.Id, user.Email, user.Location });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _context.Users
            .Include(u => u.FavoriteArtists)
            .Include(u => u.FavoriteGenres)
            .Include(u => u.SavedTracks)
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        return Ok(MapUser(user));
    }

    [HttpPost("{userId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(int userId, PreferencesDto dto)
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
            user.FavoriteArtists = await _context.Artists
                .Where(a => dto.ArtistIds.Contains(a.Id))
                .Take(10)
                .ToListAsync();
        }

        if (dto.GenreIds != null)
        {
            user.FavoriteGenres = await _context.Genres
                .Where(g => dto.GenreIds.Contains(g.Id))
                .Take(10)
                .ToListAsync();
        }

        if (dto.FavoriteSongs != null)
            user.FavoriteSongs = dto.FavoriteSongs.Take(20).ToList();

        await _context.SaveChangesAsync();

        return Ok(MapUser(user));
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

        return Ok(user.SavedTracks);
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
        var user = await _context.Users
            .Include(u => u.FavoriteArtists)
            .Include(u => u.FavoriteGenres)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        var favoriteIds = user.FavoriteArtists.Select(a => a.Id).ToHashSet();
        var recommendedArtists = new List<(Artist Artist, string Reason)>();

        // --- Genre matching ---
        var targetGenres = user.FavoriteArtists
            .SelectMany(a => a.Genres)
            .Concat(user.FavoriteGenres.Select(g => g.Name))
            .Select(g => g.ToLower())
            .Distinct()
            .ToList();

        var seedNames = user.FavoriteArtists.Select(a => a.Name).ToList();

        var allPool = await _context.Artists
            .Where(a => !favoriteIds.Contains(a.Id))
            .ToListAsync();

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
            // Parallel Spotify searches: offset 0 and offset 10 for each artist
            var searchTasks = songSourceArtists.Take(8).SelectMany(r => new[]
            {
                _spotifyService.SearchTracks($"artist:\"{r.Item1.Name}\"", 10, 0)
                    .ContinueWith(t => (Results: t.Result, r.Item1.Name, Reason: r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2)),
                _spotifyService.SearchTracks($"artist:\"{r.Item1.Name}\"", 10, 10)
                    .ContinueWith(t => (Results: t.Result, r.Item1.Name, Reason: r.IsFav ? $"By your favorite, {r.Item1.Name}" : r.Item2)),
            }).ToList();

            var batches = await Task.WhenAll(searchTasks);

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
                        track.ArtistId,
                        track.ArtistName,
                        track.SongName,
                        track.ArtistSpotifyId,
                        track.SpotifyTrackId,
                        track.ImageUrl,
                        track.PreviewUrl,
                        track.ExternalUrl,
                        Source = "Spotify",
                        Reason = b.Reason
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

        return Ok(new
        {
            Artists = recommendedArtists.Select(r => new
            {
                r.Artist.Id, r.Artist.Name, r.Artist.ImageUrl,
                Genres = r.Artist.Genres,
                r.Artist.Popularity, r.Artist.Bio,
                TopTracks = r.Artist.TopTracks,
                Reason = r.Reason
            }),
            Songs = songs
        });
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
            t.ImageUrl, t.PreviewUrl, t.ExternalUrl, t.Genres, t.Vibes, t.AddedAt
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
