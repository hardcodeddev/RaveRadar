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
        // Manual parsing to handle passwords with special characters like '@'
        var cleanUrl = url.Trim().Replace("postgresql://", "").Replace("postgres://", "");
        
        // The last '@' separates credentials from the host
        int lastAtIndex = cleanUrl.LastIndexOf('@');
        if (lastAtIndex == -1) return url;

        string credentials = cleanUrl.Substring(0, lastAtIndex);
        string hostPart = cleanUrl.Substring(lastAtIndex + 1);

        // Split credentials (user:pass)
        string user = credentials;
        string password = "";
        int firstColonIndex = credentials.IndexOf(':');
        if (firstColonIndex != -1)
        {
            user = credentials.Substring(0, firstColonIndex);
            password = credentials.Substring(firstColonIndex + 1);
        }

        // Split hostPart (host:port/database)
        string host = hostPart;
        string port = "5432";
        string database = "postgres";

        int slashIndex = hostPart.IndexOf('/');
        if (slashIndex != -1)
        {
            database = hostPart.Substring(slashIndex + 1);
            host = hostPart.Substring(0, slashIndex);
        }

        int portColonIndex = host.IndexOf(':');
        if (portColonIndex != -1)
        {
            port = host.Substring(portColonIndex + 1);
            host = host.Substring(0, portColonIndex);
        }

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error parsing DATABASE_URL: {ex.Message}");
        return url;
    }
}

