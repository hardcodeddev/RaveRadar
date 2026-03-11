using Microsoft.EntityFrameworkCore;
using Quartz;
using RaveRadar.Api.Data;
using RaveRadar.Api.Services;
using RaveRadar.Api.Quartz;

var builder = WebApplication.CreateBuilder(args);

// 1. SERVICES
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// 2. DATABASE CONFIGURATION
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        Console.WriteLine("🌐 DATABASE_URL detected, configuring PostgreSQL...");
        options.UseNpgsql(ParseDatabaseUrl(databaseUrl));
    }
    else if (connectionString?.Contains("Host=") == true || connectionString?.Contains("Server=") == true)
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        Console.WriteLine("📁 No DATABASE_URL found, using local SQLite.");
        options.UseSqlite(connectionString ?? "Data Source=RaveRadar.db");
    }
});

builder.Services.AddScoped<EdmTrainService>();
builder.Services.AddScoped<SpotifyService>();

// 3. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// 4. QUARTZ
builder.Services.AddQuartz(q =>
{
    var edmJobKey = new JobKey("EdmTrainSyncJob");
    q.AddJob<EdmTrainSyncJob>(opts => opts.WithIdentity(edmJobKey));
    q.AddTrigger(opts => opts.ForJob(edmJobKey).WithSimpleSchedule(x => x.WithIntervalInHours(12).RepeatForever()));

    var spotifyJobKey = new JobKey("SpotifyEnrichJob");
    q.AddJob<SpotifyEnrichJob>(opts => opts.WithIdentity(spotifyJobKey));
    q.AddTrigger(opts => opts.ForJob(spotifyJobKey).StartNow().WithSimpleSchedule(x => x.WithIntervalInHours(24).RepeatForever()));
});
builder.Services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

var app = builder.Build();

// 5. MIDDLEWARE
app.UseSwagger(options => { options.RouteTemplate = "api/swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "RaveRadar API V1");
    c.RoutePrefix = "api/swagger";
});

app.UseRouting();
app.UseCors("AllowAll");

// Basic API info
app.MapGet("/api", () => Results.Ok(new
{
    app = "RaveRadar API",
    status = "Healthy",
    swagger = "/api/swagger",
    database = app.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>().Database.ProviderName
}));

// Debug endpoint — shows real DB error so we can diagnose connection issues
app.MapGet("/api/debug", async (AppDbContext db) =>
{
    var connStr = db.Database.GetConnectionString() ?? "";
    var safeConn = string.Join(";", connStr.Split(';')
        .Select(p => p.TrimStart())
        .Where(p => !p.StartsWith("Password", StringComparison.OrdinalIgnoreCase)));
    try
    {
        await db.Database.OpenConnectionAsync();
        var artistCount = await db.Artists.CountAsync();
        var genreCount  = await db.Genres.CountAsync();
        return Results.Ok(new { connection = safeConn, artistCount, genreCount, status = "OK" });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            connection = safeConn,
            error  = ex.Message,
            inner  = ex.InnerException?.Message,
            inner2 = ex.InnerException?.InnerException?.Message
        });
    }
});

app.MapGet("/api/swagger", () => Results.Redirect("/api/swagger/index.html"));

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", environment = app.Environment.EnvironmentName }));

app.MapControllers();
app.MapFallbackToFile("index.html");

// 6. DB INITIALIZATION
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
        Console.WriteLine($"❌ CRITICAL ERROR: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"   INNER: {ex.InnerException.Message}");
    }
}

app.Run();

// 7. HELPERS
static string ParseDatabaseUrl(string url)
{
    try 
    {
        var cleanUrl = url.Trim().Replace("postgresql://", "").Replace("postgres://", "");
        int lastAtIndex = cleanUrl.LastIndexOf('@');
        if (lastAtIndex == -1) return url;

        string credentials = cleanUrl.Substring(0, lastAtIndex);
        string hostPart = cleanUrl.Substring(lastAtIndex + 1);

        string user = credentials;
        string password = "";
        int firstColonIndex = credentials.IndexOf(':');
        if (firstColonIndex != -1)
        {
            user = credentials.Substring(0, firstColonIndex);
            password = credentials.Substring(firstColonIndex + 1);
        }

        string host = hostPart;
        string port = "5432";
        string database = "postgres";

        int slashIndex = hostPart.IndexOf('/');
        if (slashIndex != -1)
        {
            database = hostPart.Substring(slashIndex + 1);
            host = hostPart.Substring(0, slashIndex);
        }

        // Strip query parameters (e.g. ?pgbouncer=true, ?sslmode=require) from database name
        int queryIndex = database.IndexOf('?');
        if (queryIndex != -1)
            database = database.Substring(0, queryIndex);

        int portColonIndex = host.IndexOf(':');
        if (portColonIndex != -1)
        {
            port = host.Substring(portColonIndex + 1);
            host = host.Substring(0, portColonIndex);
        }

        // Render containers don't support IPv6. Resolve the hostname to its IPv4 address
        // at startup so Npgsql never attempts an IPv6 connection.
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(host);
            var ipv4 = addresses.FirstOrDefault(a =>
                a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 != null)
            {
                Console.WriteLine($"🔍 Resolved {host} → {ipv4} (IPv4)");
                host = ipv4.ToString();
            }
            else
            {
                Console.WriteLine($"⚠️ No IPv4 address found for {host}, using hostname as-is");
            }
        }
        catch (Exception dnsEx)
        {
            Console.WriteLine($"⚠️ DNS resolution failed for {host}: {dnsEx.Message} (using hostname)");
        }

        // Port 6543 = Supabase transaction pooler (PgBouncer). It doesn't support prepared
        // statements, so disable Npgsql's own pool and tell it not to reset on close.
        var pgBouncer = port == "6543" ? ";No Reset On Close=true;Pooling=false" : "";

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true{pgBouncer}";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error parsing DATABASE_URL: {ex.Message}");
        return url;
    }
}
