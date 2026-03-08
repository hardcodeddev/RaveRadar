namespace RaveRadar.Api.Models;

public class Event
{
    public required string Id { get; set; } // Internal Unique ID (Source:SourceId)
    public required string Name { get; set; }
    public DateTime Date { get; set; }
    public string? Venue { get; set; }
    public string? City { get; set; }
    public string? TicketUrl { get; set; }
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> ArtistNames { get; set; } = new();
    public List<string> GenreNames { get; set; } = new();

    // Source Tracking
    public string? Source { get; set; } // "EdmTrain", "Ticketmaster", etc.
    public string? SourceId { get; set; }
}
