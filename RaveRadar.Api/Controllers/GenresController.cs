using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;

namespace RaveRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GenresController : ControllerBase
{
    private readonly AppDbContext _context;

    public GenresController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Genre>>> GetGenres([FromQuery] string? search)
    {
        var query = _context.Genres.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(g => g.Name.ToLower().Contains(search.ToLower()));
        }

        return await query.ToListAsync();
    }
}
