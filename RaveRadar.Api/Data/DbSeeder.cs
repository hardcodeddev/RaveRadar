using RaveRadar.Api.Models;
using System.Text.RegularExpressions;

namespace RaveRadar.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        // Ensure Genres exist
        if (!context.Genres.Any())
        {
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
            context.SaveChanges();
        }

        // Synchronize Artists
        SyncArtists(context);

        // Ensure Events exist
        if (!context.Events.Any())
        {
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

    private static void SyncArtists(AppDbContext context)
    {
        var artistsFromFile = new List<Artist>();

        // 1. Define Featured/Modern Artists (The Priority List)
        var featuredArtists = new List<Artist>
        {
            new() { 
                Name = "Levity", 
                Genres = new List<string> { "Dubstep", "Electronic" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb66b5c3e7b1a0e1c9e8e60473",
                Popularity = 88,
                Bio = "Levity is a rising electronic trio known for their high-energy performances and unique sound that blends various bass music subgenres.",
                TopTracks = new List<string> { "Flip It", "The Wheel", "Bad Habits" }
            },
            new() { 
                Name = "John Summit", 
                Genres = new List<string> { "Tech House" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb9816174a6f790c563d417934",
                Popularity = 93,
                Bio = "John Summit is a Chicago-based DJ and producer who has quickly become one of the biggest names in tech house.",
                TopTracks = new List<string> { "Where You Are", "Human", "La Danza" }
            },
            new() { 
                Name = "Fred again..", 
                Genres = new List<string> { "Electronic", "House" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb07f0f6706e90264102604928",
                Popularity = 95,
                Bio = "Fred again.. is a British record producer, singer, songwriter, multi-instrumentalist and DJ.",
                TopTracks = new List<string> { "Delilah (pull me out of this)", "Marea (weve lost dancing)", "Jungle" }
            },
            new() { 
                Name = "Dom Dolla", 
                Genres = new List<string> { "House", "Tech House" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb74f76263722971295f7ec91e",
                Popularity = 91,
                Bio = "Dom Dolla is an Australian house music producer known for his signature basslines and high-energy sets.",
                TopTracks = new List<string> { "Saving Up", "Rhyme Dust", "San Frandisco" }
            },
            new() { 
                Name = "Sara Landry", 
                Genres = new List<string> { "Hard Techno" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb817441551066927909a80536",
                Popularity = 90,
                Bio = "Sara Landry is a producer and DJ known for her dark, feminine, and industrial hard techno sound.",
                TopTracks = new List<string> { "Legacy", "Peer Pressure", "Queen of the Banshees" }
            },
            new() { 
                Name = "Knock2", 
                Genres = new List<string> { "Trap", "Bass House" }, 
                ImageUrl = "https://i.scdn.co/image/ab6761610000e5eb73004467c699949987f6312a",
                Popularity = 90,
                Bio = "Knock2 is an American DJ and producer leading the new wave of high-energy bass house and trap.",
                TopTracks = new List<string> { "dashstar*", "Rock Ur World", "Make U SWEAT!" }
            }
        };

        artistsFromFile.AddRange(featuredArtists);

        // 2. Load and parse the dataset file
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "edm_artists_dataset.txt");
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "edm_artists_dataset.txt");
        }

        if (File.Exists(filePath))
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^\d+\.\s+(.*?)\s+\((.*?)\)\s+-\s+Popularity:\s+(\d+)");
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    
                    // Skip if already in featuredArtists
                    if (artistsFromFile.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

                    var genreList = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries).ToList();
                    var popularity = int.Parse(match.Groups[3].Value);

                    artistsFromFile.Add(new Artist
                    {
                        Name = name,
                        Genres = genreList,
                        Popularity = popularity,
                        Bio = $"{name} is a renowned EDM artist known for {string.Join(" and ", genreList)} style music.",
                        TopTracks = new List<string> { "Track 1", "Track 2", "Track 3" },
                        ImageUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=random&color=fff&size=512"
                    });
                }
            }
        }

        // 3. Compare with DB and Update
        var existingArtists = context.Artists.ToList();
        var anyChanges = false;

        foreach (var artistFromFile in artistsFromFile)
        {
            var existing = existingArtists.FirstOrDefault(a => a.Name.Equals(artistFromFile.Name, StringComparison.OrdinalIgnoreCase));
            
            if (existing == null)
            {
                // New artist
                context.Artists.Add(artistFromFile);
                anyChanges = true;
            }
            else
            {
                // Update existing artist if changed
                var changed = false;
                
                if (existing.Popularity != artistFromFile.Popularity)
                {
                    existing.Popularity = artistFromFile.Popularity;
                    changed = true;
                }

                if (!existing.Genres.SequenceEqual(artistFromFile.Genres))
                {
                    existing.Genres = artistFromFile.Genres;
                    changed = true;
                }

                // If existing has a generic avatar or no image, and file has a specific one (featured list)
                if ((string.IsNullOrEmpty(existing.ImageUrl) || existing.ImageUrl.Contains("ui-avatars.com")) 
                    && !artistFromFile.ImageUrl!.Contains("ui-avatars.com"))
                {
                    existing.ImageUrl = artistFromFile.ImageUrl;
                    existing.Bio = artistFromFile.Bio;
                    existing.TopTracks = artistFromFile.TopTracks;
                    changed = true;
                }

                if (changed) anyChanges = true;
            }
        }

        if (anyChanges)
        {
            context.SaveChanges();
        }
    }
}
