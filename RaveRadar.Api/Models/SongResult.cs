namespace RaveRadar.Api.Models;

public class SongResult
{
    public int ArtistId { get; set; }
    public required string ArtistName { get; set; }
    public required string SongName { get; set; }
    public string? ArtistSpotifyId { get; set; }
    public string? SpotifyTrackId { get; set; }
    public string? ImageUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string Source { get; set; } = "local";
}
