using Microsoft.EntityFrameworkCore;
using Quartz;
using RaveRadar.Api.Data;
using RaveRadar.Api.Services;

namespace RaveRadar.Api.Quartz;

[DisallowConcurrentExecution]
public class SpotifyEnrichJob : IJob
{
    private readonly AppDbContext _context;
    private readonly SpotifyService _spotifyService;
    private readonly ILogger<SpotifyEnrichJob> _logger;

    public SpotifyEnrichJob(AppDbContext context, SpotifyService spotifyService, ILogger<SpotifyEnrichJob> logger)
    {
        _context = context;
        _spotifyService = spotifyService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_spotifyService.IsConfigured)
        {
            _logger.LogInformation("Spotify not configured — skipping artist enrichment.");
            return;
        }

        // Process a small batch per run so startup doesn't hammer Spotify with 500+ calls.
        // Unenriched artists are processed 20 at a time; the job runs every 24h so the
        // full catalogue is enriched gradually over multiple days.
        var unenriched = await _context.Artists
            .Where(a => a.SpotifyId == null)
            .Take(20)
            .ToListAsync();

        if (!unenriched.Any())
        {
            _logger.LogInformation("All artists already enriched.");
            return;
        }

        _logger.LogInformation("Enriching {Count} artists via Spotify (batch of 20)...", unenriched.Count);

        foreach (var artist in unenriched)
        {
            try
            {
                await _spotifyService.EnrichArtist(artist);
                await Task.Delay(2000); // ~30 req/min — conservative gap to avoid 429s
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich artist: {Name}", artist.Name);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Spotify enrichment batch complete. {Count} artists processed.", unenriched.Count);
    }
}
