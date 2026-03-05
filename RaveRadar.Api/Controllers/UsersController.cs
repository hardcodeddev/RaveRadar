using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;
using BCrypt.Net;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest("User already exists");
        }

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
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid credentials");
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Location,
            FavoriteArtists = user.FavoriteArtists.Select(a => new { a.Id, a.Name }),
            FavoriteGenres = user.FavoriteGenres.Select(g => new { g.Id, g.Name })
        });
    }

    [HttpPost("{userId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(int userId, PreferencesDto dto)
    {
        var user = await _context.Users
            .Include(u => u.FavoriteArtists)
            .Include(u => u.FavoriteGenres)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        user.Location = dto.Location;

        // Up to 3 artists
        if (dto.ArtistIds != null)
        {
            var artists = await _context.Artists
                .Where(a => dto.ArtistIds.Contains(a.Id))
                .Take(3)
                .ToListAsync();
            user.FavoriteArtists = artists;
        }

        // Up to 3 genres
        if (dto.GenreIds != null)
        {
            var genres = await _context.Genres
                .Where(g => dto.GenreIds.Contains(g.Id))
                .Take(3)
                .ToListAsync();
            user.FavoriteGenres = genres;
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            user.Id,
            user.Location,
            FavoriteArtists = user.FavoriteArtists.Select(a => new { a.Id, a.Name }),
            FavoriteGenres = user.FavoriteGenres.Select(g => new { g.Id, g.Name })
        });
    }
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
}
