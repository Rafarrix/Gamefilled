using System.Net;
using Gamefilled.V3.Data;
using Gamefilled.V3.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gamefilled.V3.Tests;

public sealed class IdentityFoundationTests : IClassFixture<V3ApplicationFactory>
{
    private readonly V3ApplicationFactory _factory;

    public IdentityFoundationTests(V3ApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Foundation_exposes_only_its_own_liveness_endpoint()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("gamefilled-v3", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Identity_me_rejects_anonymous_visitors()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Login", response.Headers.Location?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Requires_verified_email_and_secure_cookies()
    {
        var identity = _factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.True(identity.SignIn.RequireConfirmedEmail);
        Assert.True(identity.User.RequireUniqueEmail);
        Assert.True(identity.Password.RequiredLength >= 12);

        var cookie = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-GamefilledV3", cookie.Cookie.Name);
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, cookie.Cookie.SameSite);
    }

    [Fact]
    public void Uses_a_separate_sql_database_and_unique_email_index()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();

        Assert.Equal("GamefilledV3Tests", context.Database.GetDbConnection().Database);

        var user = context.Model.FindEntityType(typeof(GamefilledUser));
        Assert.NotNull(user);
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique && index.Properties.Any(p => p.Name == nameof(GamefilledUser.NormalizedEmail)));
    }
}

/// <summary>All connection details are test placeholders; no database is contacted.</summary>
public sealed class V3ApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _oldConnection =
        Environment.GetEnvironmentVariable("ConnectionStrings__V3Database");

    public V3ApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__V3Database",
            "Server=127.0.0.1,1;Database=GamefilledV3Tests;User Id=sa;Password=NotReal_TestOnly_123!;Encrypt=False;Connect Timeout=1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("ConnectionStrings__V3Database", _oldConnection);
    }
}
