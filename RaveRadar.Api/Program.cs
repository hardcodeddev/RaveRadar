using Microsoft.EntityFrameworkCore;
using Quartz;
using RaveRadar.Api.Data;
using RaveRadar.Api.Services;
using RaveRadar.Api.Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// Database - Support SQLite (local) and PostgreSQL (Production/Render)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Parse DATABASE_URL if provided (common in free tier providers like Supabase/Neon)
        options.UseNpgsql(ParseDatabaseUrl(databaseUrl));
    }
    else if (connectionString?.Contains("Host=") == true || connectionString?.Contains("Server=") == true)
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=RaveRadar.db");
    }
});

// Services
...
}

app.Run();

// Helper to parse DATABASE_URL (postgres://user:pass@host:port/db)
static string ParseDatabaseUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
}
builder.Services.AddScoped<EdmTrainService>();
builder.Services.AddScoped<SpotifyService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Quartz
builder.Services.AddQuartz(q =>
{
    // EDM Train sync — every 12 hours
    var edmJobKey = new JobKey("EdmTrainSyncJob");
    q.AddJob<EdmTrainSyncJob>(opts => opts.WithIdentity(edmJobKey));
    q.AddTrigger(opts => opts
        .ForJob(edmJobKey)
        .WithIdentity("EdmTrainSyncJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInHours(12).RepeatForever())
    );

    // Spotify enrichment — runs on startup then every 24 hours
    var spotifyJobKey = new JobKey("SpotifyEnrichJob");
    q.AddJob<SpotifyEnrichJob>(opts => opts.WithIdentity(spotifyJobKey));
    q.AddTrigger(opts => opts
        .ForJob(spotifyJobKey)
        .WithIdentity("SpotifyEnrichJob-trigger")
        .StartNow()
        .WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever())
    );
});
builder.Services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

var app = builder.Build();

// Enable Swagger in all environments for debugging
app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaveRadar API V1");
    c.RoutePrefix = "swagger"; // reach it at /swagger
});

app.UseRouting();
app.UseCors("AllowClient");

// Basic API info/health endpoint
app.MapGet("/api", () => Results.Ok(new 
{ 
    app = "RaveRadar API", 
    status = "Healthy", 
    swagger = "/swagger",
    environment = app.Environment.EnvironmentName
}));

// In Docker/Render, SSL is handled at the load balancer
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", environment = app.Environment.EnvironmentName }));

app.MapControllers();
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine("🚀 Running migrations and seeding database...");
    try 
    {
        context.Database.Migrate();
        DbSeeder.Seed(context);
        Console.WriteLine("✅ Database initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error during startup: {ex.Message}");
    }
}

app.Run();
