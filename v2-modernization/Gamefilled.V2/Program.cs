using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Library;
using Gamefilled.V2.Games;
using Gamefilled.V2.Security;
using Gamefilled.Application.Email;
using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Deliberately different from V2's DefaultConnection / Gamefilleddb.
var connection = builder.Configuration.GetConnectionString("V2ModernDatabase");
if (string.IsNullOrWhiteSpace(connection))
{
    throw new InvalidOperationException(
        "Gamefilled V2 modernization requires ConnectionStrings:V2ModernDatabase for an isolated SQL database.");
}

// Only preview uses file-system keys here. Keep login and email tokens
// valid after a container restart. Live production will use a reviewed key store.
if (builder.Environment.IsEnvironment("Preview"))
{
    var keyPath = builder.Configuration["DataProtection:KeysDirectory"];
    if (string.IsNullOrWhiteSpace(keyPath)
        || !Path.IsPathFullyQualified(keyPath)
        || !Directory.Exists(keyPath))
        throw new InvalidOperationException("Preview Data Protection key directory must exist.");

    builder.Services.AddDataProtection()
        .SetApplicationName("Gamefilled-V2-Preview")
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

builder.Services.AddRazorPages();
builder.Services.AddScoped<GameLibraryService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<IgdbOptions>(builder.Configuration.GetSection("IGDB"));
builder.Services.AddHttpClient("TwitchAuth");
builder.Services.AddSingleton<IgdbTokenProvider>();
builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<ILibraryGameCatalog, IgdbLibraryGameCatalog>();
builder.Services.AddScoped<IGameDetailCatalog, IgdbGameDetailCatalog>();
builder.Services.AddScoped<IGameSearchCatalog, IgdbGameSearchCatalog>();
builder.Services.AddDbContext<GamefilledIdentityDbContext>(options =>
    options.UseSqlServer(connection));

builder.Services.AddIdentityCore<GamefilledUser>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<GamefilledIdentityDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

builder.Services.AddSingleton<AccountLinkFactory>();
builder.Services.Configure<ResendEmailOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddHttpClient<ITransactionalEmailSender, ResendEmailSender>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Rendering a GET form must not consume the small POST/send allowance.
    options.AddPolicy("account-forms", context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("account-form-read")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 25,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    // Mutating library actions are bounded by authenticated user or network origin.
    // Browsing the library must not consume the write allowance.
    options.AddPolicy("library-write", context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("library-read")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.IsAuthenticated == true
                    ? "library:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown")
                    : "library-anon:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    // Profile edits are isolated by authenticated Identity user and bounded.
    options.AddPolicy("profile-write", context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("profile-read")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.IsAuthenticated == true
                    ? "profile:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown")
                    : "profile-anon:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 12,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    // Anonymous public search has a bounded per-client budget.
    options.AddPolicy("catalog-search", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "search:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("email-send", context =>
        !HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetNoLimiter("email-form-read")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
});

// Cookie-only by design. No bearer/refresh-token endpoint or legacy session auth.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-GamefilledV2Modern";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});
builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    var trusted = builder.Configuration["ReverseProxy:TrustedNetwork"];
    if (!string.IsNullOrWhiteSpace(trusted))
    {
        if (!System.Net.IPNetwork.TryParse(trusted, out var parsed))
            throw new InvalidOperationException("Invalid ReverseProxy trusted CIDR.");
        options.KnownIPNetworks.Add(parsed);
    }
});

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    // Never expose stack traces to clients outside local development.
    app.UseExceptionHandler(errorApp =>
        errorApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Modernization routes remain isolated from current V2; not deployed publicly.
// Account recovery and richer user flows will follow after staging SQL validation.
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "gamefilled-v2-modern" }))
    .AllowAnonymous();

// Preview readiness is separate from liveness; never disclose SQL details.
app.MapGet("/health/ready", async (
    GamefilledIdentityDbContext database,
    CancellationToken cancellationToken) =>
{
    try
    {
        return await database.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapGet("/identity/me", (System.Security.Claims.ClaimsPrincipal principal) =>
{
    return Results.Ok(new { id = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value });
}).RequireAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program { }
