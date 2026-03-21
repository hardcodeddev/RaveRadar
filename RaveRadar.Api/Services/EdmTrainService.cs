using Microsoft.EntityFrameworkCore;
using RaveRadar.Api.Data;
using RaveRadar.Api.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaveRadar.Api.Services;

public class EdmTrainService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EdmTrainService> _logger;

    public EdmTrainService(
        AppDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<EdmTrainService> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SyncEvents(string? state = null, string? city = null)
    {
        var apiKey = _configuration["EdmTrain:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_EDMTRAIN_API_KEY")
        {
            _logger.LogWarning("EDM Train API key is not configured.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        var url = $"https://edmtrain.com/api/events?client={apiKey}";
        
        if (!string.IsNullOrEmpty(state)) url += $"&state={Uri.EscapeDataString(state)}";
        if (!string.IsNullOrEmpty(city)) url += $"&city={Uri.EscapeDataString(city)}";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var edmResponse = JsonSerializer.Deserialize<EdmTrainResponse>(json);

            if (edmResponse?.Success == true && edmResponse.Data != null)
            {
                await ProcessEvents(edmResponse.Data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing events from EDM Train");
        }
    }

    private async Task ProcessEvents(List<EdmEvent> edmEvents)
    {
        // Load all existing IDs in one query to avoid N+1 FindAsync calls
        var incomingIds = edmEvents.Select(e => $"EdmTrain:{e.Id}").ToHashSet();
        var existingIds = (await _context.Events
            .Where(e => incomingIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync())
            .ToHashSet();

        // Track IDs staged for insert in this batch to avoid within-batch duplicates
        var stagedIds = new HashSet<string>();

        foreach (var edmEvent in edmEvents)
        {
            var internalId = $"EdmTrain:{edmEvent.Id}";
            var artistNames = edmEvent.ArtistList?.Select(a => a.Name).ToList() ?? new List<string>();
            var eventName = edmEvent.Name ?? (artistNames.Any() ? string.Join(", ", artistNames) : "EDM Event");
            var eventDate = DateTime.TryParse(edmEvent.Date, out var date) ? date : DateTime.Now;

            if (existingIds.Contains(internalId))
            {
                // Update — FindAsync hits the local cache first so no extra DB round-trip
                var existing = await _context.Events.FindAsync(internalId);
                if (existing != null)
                {
                    existing.Name = eventName;
                    existing.Date = eventDate;
                    existing.Venue = edmEvent.Venue?.Name;
                    existing.City = edmEvent.Venue?.Location;
                    existing.TicketUrl = edmEvent.Link;
                    existing.ArtistNames = artistNames;
                    existing.Latitude = edmEvent.Venue?.Latitude ?? 0;
                    existing.Longitude = edmEvent.Venue?.Longitude ?? 0;
                }
            }
            else if (stagedIds.Add(internalId))
            {
                // New event — only add once even if API returns duplicate IDs
                _context.Events.Add(new Event
                {
                    Id = internalId,
                    Name = eventName,
                    Date = eventDate,
                    Venue = edmEvent.Venue?.Name,
                    City = edmEvent.Venue?.Location,
                    TicketUrl = edmEvent.Link,
                    Latitude = edmEvent.Venue?.Latitude ?? 0,
                    Longitude = edmEvent.Venue?.Longitude ?? 0,
                    ArtistNames = artistNames,
                    GenreNames = new List<string> { "Electronic" },
                    Source = "EdmTrain",
                    SourceId = edmEvent.Id.ToString(),
                    ImageUrl = $"https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&q=80&w=1000&sig={edmEvent.Id}"
                });
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint") == true
                                           || ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            // Concurrent sync job already inserted these rows — safe to discard this batch
            _logger.LogWarning("Skipping duplicate events from concurrent sync run.");
            _context.ChangeTracker.Clear();
        }
    }
}

// DTOs for EDM Train Response
public class EdmTrainResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("data")]
    public List<EdmEvent>? Data { get; set; }
}

public class EdmEvent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("date")]
    public string? Date { get; set; }
    
    [JsonPropertyName("link")]
    public string? Link { get; set; }
    
    [JsonPropertyName("venue")]
    public EdmVenue? Venue { get; set; }
    
    [JsonPropertyName("artistList")]
    public List<EdmArtist>? ArtistList { get; set; }
}

public class EdmVenue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }
    
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public class EdmArtist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
