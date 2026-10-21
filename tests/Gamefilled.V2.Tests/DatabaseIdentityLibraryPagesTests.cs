using System.Net;
using System.Text.RegularExpressions;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class DatabaseIdentityLibraryPagesTests
{
    [Fact]
    public async Task Library_forms_require_identity_and_csrf_and_preserve_account_ownership()
    {
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__V2ModernDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connection));

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            // The host deliberately surfaces unhandled exceptions in Testing only.
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:V2ModernDatabase", connection);
        });

        var emailA = Guid.NewGuid().ToString("N") + "@example.invalid";
        var emailB = Guid.NewGuid().ToString("N") + "@example.invalid";
        const string password = "valid-identity-test-passphrase";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledUser>>();
            foreach (var email in new[] { emailA, emailB })
            {
                var created = await users.CreateAsync(new GamefilledUser
                {
                    Id = Guid.NewGuid(),
                    UserName = "library" + Guid.NewGuid().ToString("N")[..12],
                    Email = email,
                    EmailConfirmed = true
                }, password);
                Assert.True(created.Succeeded);
            }
        }

        using var anonymous = Client(factory);
        using var blocked = await anonymous.GetAsync("/library/all");
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);

        using var a = Client(factory);
        using var b = Client(factory);
        await SignInAsync(a, emailA, password);
        await SignInAsync(b, emailB, password);

        using (var page = await a.GetAsync("/library/all"))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("no-store", page.Headers.CacheControl?.ToString());
            Assert.Contains("gf-library-page", await page.Content.ReadAsStringAsync());
        }

        // A valid authentication cookie does not bypass antiforgery validation.
        using (var csrfMissing = await a.PostAsync("/library/all?handler=Status",
            Form(("gameId", "7301"), ("status", "playing"))))
        {
            Assert.Equal(HttpStatusCode.BadRequest, csrfMissing.StatusCode);
        }

        await PostFormAsync(a, "/library/all", "/library/all?handler=Status",
            ("gameId", "7301"), ("status", "playing"), ("section", "all"));

        using (var aPage = await a.GetAsync("/library/playing"))
        {
            var body = await aPage.Content.ReadAsStringAsync();
            Assert.Contains("IGDB #7301", body);
            Assert.Contains("playing", body);
        }

        using (var bPage = await b.GetAsync("/library/all"))
        {
            Assert.DoesNotContain("IGDB #7301", await bPage.Content.ReadAsStringAsync());
        }

        await PostFormAsync(a, "/library/playing", "/library/playing?handler=Review",
            ("gameId", "7301"), ("rating", "9"),
            ("reviewText", "Really enjoyed this game"),
            ("containsSpoilers", "true"), ("section", "playing"));

        await PostFormAsync(a, "/library/playing", "/library/playing?handler=Status",
            ("gameId", "7301"), ("status", "played"), ("section", "playing"));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            var entry = await db.GameLibraryEntries.SingleAsync(x => x.IgdbGameId == 7301);
            var userA = await scope.ServiceProvider
                .GetRequiredService<UserManager<GamefilledUser>>().FindByEmailAsync(emailA);

            Assert.Equal(userA!.Id, entry.UserId);
            Assert.Equal("played", entry.Status);
            Assert.Equal(9, entry.Rating);
            Assert.True(entry.ContainsSpoilers);
            Assert.Equal("Really enjoyed this game", entry.ReviewText);
        }

        // B cannot remove A's game using the very same IGDB game ID.
        await PostFormAsync(b, "/library/all", "/library/all?handler=Remove",
            ("gameId", "7301"), ("section", "all"));

        using (var aPage = await a.GetAsync("/library/played"))
        {
            Assert.Contains("IGDB #7301", await aPage.Content.ReadAsStringAsync());
        }

        // GET never removes or changes an entry.
        using (var getOnly = await a.GetAsync("/library/all?handler=Remove&gameId=7301"))
        {
            Assert.Equal(HttpStatusCode.OK, getOnly.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.Equal(1, await db.GameLibraryEntries.CountAsync(x => x.IgdbGameId == 7301));
        }

        await PostFormAsync(a, "/library/all", "/library/all?handler=Remove",
            ("gameId", "7301"), ("section", "all"));
        using (var aPage = await a.GetAsync("/library/all"))
        {
            Assert.DoesNotContain("IGDB #7301", await aPage.Content.ReadAsStringAsync());
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://dev.gamefilled.pt"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        await PostFormAsync(client, "/account/login", "/account/login",
            ("Email", email), ("Password", password));
        using var identity = await client.GetAsync("/identity/me");
        Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
    }

    private static async Task PostFormAsync(HttpClient client, string getUrl, string postUrl,
        params (string Name, string Value)[] form)
    {
        using var page = await client.GetAsync(getUrl);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html,
            "<input(?=[^>]*name=\"__RequestVerificationToken\")(?=[^>]*value=\"([^\"]+)\")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "A Razor POST form must contain the antiforgery token.");
        var fields = form.Select(item => new KeyValuePair<string, string>(item.Name, item.Value))
            .Append(new KeyValuePair<string, string>("__RequestVerificationToken",
                System.Net.WebUtility.HtmlDecode(token.Groups[1].Value)));

        using var posted = await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields) =>
        new(fields.Select(item => new KeyValuePair<string, string>(item.Name, item.Value)));
}
