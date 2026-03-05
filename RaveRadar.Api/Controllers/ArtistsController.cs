using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ArtistsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Artist>>> GetArtists([FromQuery] string? search)
    {
        var query = _context.Artists.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.Name.ToLower().Contains(search.ToLower()));
        }

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Artist>> GetArtist(int id)
    {
        var artist = await _context.Artists.FindAsync(id);

        if (artist == null)
        {
            return NotFound();
        }

        return artist;
    }
}
