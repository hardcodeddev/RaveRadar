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
builder.Services.AddScoped<EdmTrainService>();
builder.Services.AddScoped<SpotifyService>();

// CORS - Allow any origin for the public API demo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Quartz
builder.Services.AddQuartz(q =>
{
    var edmJobKey = new JobKey("EdmTrainSyncJob");
    q.AddJob<EdmTrainSyncJob>(opts => opts.WithIdentity(edmJobKey));
    q.AddTrigger(opts => opts
        .ForJob(edmJobKey)
        .WithIdentity("EdmTrainSyncJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInHours(12).RepeatForever())
    );

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

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "RaveRadar API V1");
    c.RoutePrefix = "api/swagger";
});

app.UseRouting();
app.UseCors("AllowAll");

// Redirect from /api/swagger to the actual index page
app.MapGet("/api/swagger", () => Results.Redirect("/api/swagger/index.html"));

app.MapGet("/api", () => Results.Ok(new 
{ 
    app = "RaveRadar API", 
    status = "Healthy", 
    swagger = "/swagger",
    environment = app.Environment.EnvironmentName
}));

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", environment = app.Environment.EnvironmentName }));

app.MapControllers();
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbType = context.Database.IsNpgsql() ? "PostgreSQL" : "SQLite";
    Console.WriteLine($"🚀 Starting up on {app.Environment.EnvironmentName} environment...");
    Console.WriteLine($"📦 Using Database: {dbType}");
    
    try 
    {
        Console.WriteLine("🔄 Running migrations...");
        context.Database.Migrate();
        Console.WriteLine("🌱 Seeding database...");
        DbSeeder.Seed(context);
        Console.WriteLine("✅ Database initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ CRITICAL: Error during startup: {ex.Message}");
        if (ex.InnerException != null) 
            Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
    }
}

app.Run();
// Helper to parse DATABASE_URL (postgres://user:pass@host:port/db)
static string ParseDatabaseUrl(string url)
{
    try 
    {
        // Handle postgresql:// and ensure URI format
        var cleanUrl = url.Trim().Replace("postgresql://", "postgres://");
        if (!cleanUrl.StartsWith("postgres://")) cleanUrl = "postgres://" + cleanUrl;

        var uri = new Uri(cleanUrl);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var database = uri.AbsolutePath.Trim('/');

        // Return Npgsql formatted connection string
        return $"Host={uri.Host};Port={uri.Port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error parsing DATABASE_URL: {ex.Message}");
        return url; // Fallback to raw string if parsing fails
    }
}

