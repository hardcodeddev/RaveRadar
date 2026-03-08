namespace RaveRadar.Api.Models;

public class SavedTrack
{
    public int Id { get; set; }
    public string? SpotifyTrackId { get; set; }
    public required string SongName { get; set; }
    public required string ArtistName { get; set; }
    public string? ArtistSpotifyId { get; set; }
    public string? ImageUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Vibes { get; set; } = new();
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
}
