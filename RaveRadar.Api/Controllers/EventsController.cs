using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Event>>> GetEvents([FromQuery] string? city, [FromQuery] int? userId)
    {
        var query = _context.Events.AsQueryable();

        if (userId.HasValue)
        {
            var user = await _context.Users
                .Include(u => u.FavoriteArtists)
                .Include(u => u.FavoriteGenres)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user != null)
            {
                var events = await _context.Events.ToListAsync();
                
                // Score events based on preferences
                var scoredEvents = events.Select(e => new
                {
                    Event = e,
                    Score = CalculateScore(e, user)
                })
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Event.Date)
                .Select(s => s.Event)
                .ToList();

                return scoredEvents;
            }
        }

        if (!string.IsNullOrEmpty(city))
        {
            query = query.Where(e => e.City != null && e.City.ToLower() == city.ToLower());
        }

        return await query.ToListAsync();
    }

    private int CalculateScore(Event e, User user)
    {
        int score = 0;

        // Location match (High priority)
        if (!string.IsNullOrEmpty(user.Location) && e.City != null && e.City.ToLower() == user.Location.ToLower())
        {
            score += 100;
        }

        // Artist match
        if (user.FavoriteArtists.Any())
        {
            var favArtistNames = user.FavoriteArtists.Select(a => a.Name.ToLower()).ToList();
            if (e.ArtistNames.Any(an => favArtistNames.Contains(an.ToLower())))
            {
                score += 50;
            }
        }

        // Genre match
        if (user.FavoriteGenres.Any())
        {
            var favGenreNames = user.FavoriteGenres.Select(g => g.Name.ToLower()).ToList();
            if (e.GenreNames.Any(gn => favGenreNames.Contains(gn.ToLower())))
            {
                score += 30;
            }
        }

        return score;
    }
}
