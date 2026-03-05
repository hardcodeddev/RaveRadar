namespace RaveRadar.Api.Models;

public class Artist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? SpotifyId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Genres { get; set; } = new();
}
