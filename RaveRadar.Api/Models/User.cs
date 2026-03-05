namespace RaveRadar.Api.Models;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? Location { get; set; }
    public List<Artist> FavoriteArtists { get; set; } = new();
    public List<Genre> FavoriteGenres { get; set; } = new();
}
