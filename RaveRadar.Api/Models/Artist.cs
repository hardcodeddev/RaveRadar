namespace RaveRadar.Api.Models;

public class Artist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? SpotifyId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Genres { get; set; } = new();
    public int Popularity { get; set; }
    public string? Bio { get; set; }
    public List<string> TopTracks { get; set; } = new();
}
