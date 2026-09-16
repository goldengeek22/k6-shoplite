using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using ShopLite.Api.Auth;
using ShopLite.Api.Chaos;
using ShopLite.Api.Data;
using ShopLite.Api.Domain;
using ShopLite.Api.Endpoints;
using ShopLite.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --------------- Logging ---------------
builder.Logging.AddSimpleConsole(options =>
{
   options.IncludeScopes = true; // shows requestId from RequestIdMiddleware
   options.SingleLine = true;
   options.TimestampFormat = "HH:mm:ss.fff "; 
});

// --------------- Database ---------------
builder.Services.AddDbContext<ShopDbContext>(options =>
{
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention();
});

// --------------- Authentication ---------------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Missing 'jwt' configuration section");

if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes for HMAC-SHA256");
}

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
       options.MapInboundClaims = false;    // keep 'sub' as 'sub' instead of a long XML claim type
       options.TokenValidationParameters = new TokenValidationParameters
       {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromSeconds(30)   
       };
    });

builder.Services.AddAuthorization();

// ---------- Chaos + errors ----------
builder.Services.AddSingleton<ChaosState>();
builder.Services.AddProblemDetails();

// ---------- Metrics (Prometheus on /metrics) ----------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("shoplite-api"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()     // http.server.request.duration, active requests
        .AddRuntimeInstrumentation()        // GC, thread pool, allocations
        .AddMeter("Npgsql")                 // connection pool usage, command durations
        .AddPrometheusExporter()
    );

var app = builder.Build();

// ---------- Apply migrations and seed data ----------
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    await db.Database.MigrateAsync();

    var productCount = app.Configuration.GetValue("Seed:ProductCount", 10_000);
    await DataSeeder.SeedAsync(db, productCount, logger);
}

// ---------- Middleware pipeline ----------
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ChaosMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// ---------- Endpoints ----------
app.MapGet("/health", async (ShopDbContext db, CancellationToken cancellationToken) =>

   await db.Database.CanConnectAsync(cancellationToken) 
   ? Results.Ok(new { status = "UP"}) 
   : Results.Json(new { status = "DOWN"}, statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();

if (app.Configuration.GetValue<bool>("Chaos:Enabled"))
{
    app.MapAdminEndpoints();
    app.Logger.LogWarning("Chaos admin endpoints are ENABLED. Never enable this in production");
}

app.MapPrometheusScrapingEndpoint(); // GET /metrics

app.Run();
