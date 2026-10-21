using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Deliberately different from V2's DefaultConnection / Gamefilleddb.
var connection = builder.Configuration.GetConnectionString("V2ModernDatabase");
if (string.IsNullOrWhiteSpace(connection))
{
    throw new InvalidOperationException(
        "Gamefilled V2 modernization requires ConnectionStrings:V2ModernDatabase for an isolated SQL database.");
}

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

if (!app.Environment.IsDevelopment())
{
    // Never leak exception details to callers in Production.
    app.UseExceptionHandler(errorApp =>
        errorApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// The modernized V2 foundation has NO registration, login, password reset, or public
// mail endpoints yet. Those require reviewed migrations and integration tests.
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "gamefilled-v2-modern" }))
    .AllowAnonymous();

app.MapGet("/identity/me", (System.Security.Claims.ClaimsPrincipal principal) =>
{
    return Results.Ok(new { id = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value });
}).RequireAuthorization();

app.Run();

public partial class Program { }
