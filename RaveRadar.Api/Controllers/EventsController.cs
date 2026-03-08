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
    public async Task<ActionResult<IEnumerable<Event>>> GetEvents([FromQuery] string? city, [FromQuery] int? userId, [FromQuery] bool allCities = false)
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
                // Filter by city if the user has a location set and allCities is not requested
                var eventsQuery = _context.Events.AsQueryable();
                var effectiveCity = city ?? user.Location;

                if (!allCities && !string.IsNullOrEmpty(effectiveCity))
                {
                    eventsQuery = eventsQuery.Where(e => e.City != null && e.City.ToLower() == effectiveCity.ToLower());
                }

                var events = await eventsQuery.ToListAsync();

                // Score events based on preferences
                var scoredEvents = events.Select(e =>
                {
                    var (score, reason) = ScoreWithReason(e, user);
                    return new { Event = e, Score = score, Reason = reason };
                })
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Event.Date)
                .Select(s => new
                {
                    s.Event.Id, s.Event.Name, s.Event.Date, s.Event.Venue,
                    s.Event.City, s.Event.TicketUrl, s.Event.ImageUrl,
                    s.Event.Latitude, s.Event.Longitude,
                    s.Event.ArtistNames, s.Event.GenreNames,
                    s.Reason
                })
                .ToList();

                return Ok(scoredEvents);
            }
        }

        if (!string.IsNullOrEmpty(city))
        {
            query = query.Where(e => e.City != null && e.City.ToLower() == city.ToLower());
        }

        return await query.ToListAsync();
    }

    private static (int Score, string? Reason) ScoreWithReason(Event e, User user)
    {
        int score = 0;
        var reasons = new List<string>();

        var favArtistNames = user.FavoriteArtists.Select(a => a.Name.ToLower()).ToList();
        var matchedArtist = e.ArtistNames.FirstOrDefault(an => favArtistNames.Contains(an.ToLower()));
        if (matchedArtist != null)
        {
            score += 50;
            reasons.Add($"Features {matchedArtist}");
        }

        var favGenreNames = user.FavoriteGenres.Select(g => g.Name.ToLower()).ToList();
        var matchedGenre = e.GenreNames.FirstOrDefault(gn => favGenreNames.Contains(gn.ToLower()));
        if (matchedGenre != null)
        {
            score += 30;
            reasons.Add($"{matchedGenre} event");
        }

        if (!string.IsNullOrEmpty(user.Location) && e.City != null &&
            e.City.ToLower() == user.Location.ToLower())
        {
            score += 100;
            if (reasons.Count == 0) reasons.Add($"Near you in {e.City}");
        }

        return (score, reasons.Count > 0 ? string.Join(" · ", reasons) : null);
    }
}
