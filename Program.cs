using Gamefilled.Application.Companies;
using Gamefilled.Application.Games;
using Gamefilled.Application.Email;
using Gamefilled.Application.Notifications;
using Gamefilled.Application.Social;
using Gamefilled.Application.Security;
using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:DefaultConnection'. " +
        "Configure it with user secrets, an ignored appsettings file, or an environment variable.");
}

/* ============================================================================
   RAZOR PAGES
   ============================================================================ */
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/Settings", model =>
    {
        model.Filters.Add(new RequireLoginFilter());
    });
});

/* ============================================================================
   ENTITY FRAMEWORK CORE + SQL SERVER
   ============================================================================ */
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

/* ============================================================================
   IGDB / TWITCH
   ============================================================================ */
builder.Services
    .AddOptions<IgdbOptions>()
    .Bind(builder.Configuration.GetSection("IGDB"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId),
        "IGDB:ClientId is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret),
        "IGDB:ClientSecret is required.")
    .ValidateOnStart();

builder.Services.AddHttpClient("TwitchAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<IgdbTokenProvider>();

builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IgdbApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Not used by the existing auth flow. The future Identity implementation will
// call this transport; missing API credentials fail closed at send time.
builder.Services.Configure<ResendEmailOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient<ITransactionalEmailSender, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddScoped<IGameDiscoveryService, GameDiscoveryService>();
builder.Services.AddScoped<UserLibraryService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<SocialGraphService>();
builder.Services.AddScoped<SocialListService>();
builder.Services.AddScoped<NotificationService>();

/* ============================================================================
   CACHE + SESSION
   ============================================================================ */
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Gamefilled.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(1);
});

/* ============================================================================
   PROXY TRUST AND REQUEST THROTTLING
   ============================================================================ */
// Only accept client IP and HTTPS scheme headers from explicitly trusted proxy networks.
// Configure ReverseProxy__TrustedNetwork in the deployment (e.g., the dedicated Docker subnet).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var trustedNetwork = builder.Configuration["ReverseProxy:TrustedNetwork"];
    if (!string.IsNullOrWhiteSpace(trustedNetwork))
    {
        if (!System.Net.IPNetwork.TryParse(trustedNetwork, out var network))
            throw new InvalidOperationException("Invalid ReverseProxy:TrustedNetwork CIDR.");

        options.KnownIPNetworks.Add(network);
    }
});

builder.Services.AddSingleton<LoginAttemptGuard>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login-ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "login:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("registration-ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "register:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

/* ============================================================================
   PLATFORM SERVICES
   ============================================================================ */
builder.Services.AddHealthChecks();
builder.Services.AddScoped<RequireLoginFilter>();

var app = builder.Build();

// Must run before HTTPS redirection, session and endpoint rate limiting.
app.UseForwardedHeaders();

/* ============================================================================
   HTTP PIPELINE
   ============================================================================ */
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd(
        "Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=()");

    await next();
});

app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseCookiePolicy();
app.UseSession();
app.UseMiddleware<UserPresenceMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "healthy",
    service = "gamefilled",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var databaseReady = await db.Database.CanConnectAsync(ct);
        if (databaseReady)
        {
            return Results.Ok(new
            {
                status = "ready",
                database = "reachable",
                timestamp = DateTimeOffset.UtcNow
            });
        }

        return Results.Json(
            new { status = "not-ready", database = "unreachable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.Json(
            new { status = "not-ready", database = "unreachable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapRazorPages();

app.Run();

// Exposes the generated top-level Program type to WebApplicationFactory in tests.
public partial class Program
{
}
