using RaveRadar.Api.Models;

namespace RaveRadar.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Artists.Any()) return;

        var genres = new List<Genre>
        {
            new() { Name = "House" },
            new() { Name = "Techno" },
            new() { Name = "Dubstep" },
            new() { Name = "Trance" },
            new() { Name = "Drum & Bass" },
            new() { Name = "Trap" },
            new() { Name = "Future Bass" }
        };

        context.Genres.AddRange(genres);

        var artists = new List<Artist>
        {
            new() { Name = "Martin Garrix", Genres = new List<string> { "House", "Future Bass" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb817c271876535d4911c75017" },
            new() { Name = "Illenium", Genres = new List<string> { "Future Bass", "Dubstep" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb3066df423e20129753c07e05" },
            new() { Name = "Excision", Genres = new List<string> { "Dubstep" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb26d17926c04f323c65c26b2d" },
            new() { Name = "Tiësto", Genres = new List<string> { "House", "Trance" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb09ec784c1f9c8d356610037a" },
            new() { Name = "Charlotte de Witte", Genres = new List<string> { "Techno" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb143715dfc37a671231dfb8e2" },
            new() { Name = "Sub Focus", Genres = new List<string> { "Drum & Bass" }, ImageUrl = "https://i.scdn.co/image/ab6761610000e5ebb7225c5067425893b069d500" }
        };

        context.Artists.AddRange(artists);
        
        var events = new List<Event>
        {
             new() { 
                 Id = "1", 
                 Name = "Ultra Music Festival", 
                 Date = DateTime.Now.AddDays(30), 
                 Venue = "Bayfront Park", 
                 City = "Miami", 
                 Latitude = 25.78, 
                 Longitude = -80.18, 
                 TicketUrl = "https://ultramusicfestival.com",
                 ImageUrl = "https://images.unsplash.com/photo-1533174072545-e8d4aa97edf9?auto=format&fit=crop&q=80&w=1000",
                 ArtistNames = new List<string> { "Martin Garrix", "Tiësto" },
                 GenreNames = new List<string> { "House", "Trance" }
             },
             new() { 
                 Id = "2", 
                 Name = "EDC Las Vegas", 
                 Date = DateTime.Now.AddDays(60), 
                 Venue = "Las Vegas Motor Speedway", 
                 City = "Las Vegas", 
                 Latitude = 36.27, 
                 Longitude = -115.01,
                 TicketUrl = "https://lasvegas.electricdaisycarnival.com/",
                 ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&q=80&w=1000",
                 ArtistNames = new List<string> { "Excision", "Illenium" },
                 GenreNames = new List<string> { "Dubstep", "Future Bass" }
             }
        };
        context.Events.AddRange(events);

        context.SaveChanges();
    }
}
