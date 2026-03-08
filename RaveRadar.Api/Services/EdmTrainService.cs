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
        foreach (var edmEvent in edmEvents)
        {
            var internalId = $"EdmTrain:{edmEvent.Id}";
            var existing = await _context.Events.FindAsync(internalId);

            var artistNames = edmEvent.ArtistList?.Select(a => a.Name).ToList() ?? new List<string>();
            
            // Try to infer genres from name or artists (simplified)
            var genreNames = new List<string> { "Electronic" }; 

            var newEvent = new Event
            {
                Id = internalId,
                Name = edmEvent.Name ?? (artistNames.Any() ? string.Join(" , ", artistNames) : "EDM Event"),
                Date = DateTime.TryParse(edmEvent.Date, out var date) ? date : DateTime.Now,
                Venue = edmEvent.Venue?.Name,
                City = edmEvent.Venue?.Location,
                TicketUrl = edmEvent.Link,
                Latitude = edmEvent.Venue?.Latitude ?? 0,
                Longitude = edmEvent.Venue?.Longitude ?? 0,
                ArtistNames = artistNames,
                GenreNames = genreNames,
                Source = "EdmTrain",
                SourceId = edmEvent.Id.ToString(),
                // EdmTrain doesn't provide images, use a nice EDM-themed placeholder
                ImageUrl = $"https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&q=80&w=1000&sig={edmEvent.Id}"
            };

            if (existing == null)
            {
                _context.Events.Add(newEvent);
            }
            else
            {
                // Update existing
                existing.Name = newEvent.Name;
                existing.Date = newEvent.Date;
                existing.Venue = newEvent.Venue;
                existing.City = newEvent.City;
                existing.TicketUrl = newEvent.TicketUrl;
                existing.ArtistNames = newEvent.ArtistNames;
                existing.Latitude = newEvent.Latitude;
                existing.Longitude = newEvent.Longitude;
            }
        }

        await _context.SaveChangesAsync();
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
