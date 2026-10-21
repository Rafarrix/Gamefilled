using System.Net;
using System.Text.RegularExpressions;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Library;
using Gamefilled.V2.Games;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            host.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILibraryGameCatalog>();
                services.AddSingleton<ILibraryGameCatalog>(new FakeLibraryCatalog());
                services.RemoveAll<IGameDetailCatalog>();
                services.AddSingleton<IGameDetailCatalog>(new FakeGameDetailCatalog());
            });
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

        using (var guestDetail = await anonymous.GetAsync("/games/9302"))
        {
            Assert.Equal(HttpStatusCode.OK, guestDetail.StatusCode);
            var html = await guestDetail.Content.ReadAsStringAsync();
            Assert.Contains("Example Game 9302", html);
            Assert.Contains("Games you may like", html);
            Assert.DoesNotContain("<script>alert", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("href=\"/account/login\"", html);
        }
        using (var missing = await anonymous.GetAsync("/games/929299"))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

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
            Assert.Contains("Test game from IGDB", body);
            Assert.Contains("https://images.igdb.com/igdb/image/upload/t_cover_big/co123.jpg", body);
            Assert.Contains("href=\"/games/7301\"", body);
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

        // Game detail is the preferred add-to-library entry point. It may
        // display public IGDB data, but only the signed-in viewer may write.
        using (var noCsrf = await a.PostAsync("/games/9302?handler=Status",
            Form(("status", "playing"))))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        }
        await PostFormAsync(a, "/games/9302", "/games/9302?handler=Status",
            ("status", "playing"));
        using (var ownDetail = await a.GetAsync("/games/9302"))
        {
            Assert.Equal(HttpStatusCode.OK, ownDetail.StatusCode);
            Assert.Contains("aria-pressed=\"true\"", await ownDetail.Content.ReadAsStringAsync());
        }
        await PostFormAsync(a, "/games/9302", "/games/9302?handler=Review",
            ("rating", "8"), ("reviewText", "Nice game"), ("containsSpoilers", "true"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            var entry = await db.GameLibraryEntries.SingleAsync(x => x.IgdbGameId == 9302);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledUser>>();
            var userA = await users.FindByEmailAsync(emailA);
            Assert.Equal(userA!.Id, entry.UserId);
            Assert.Equal("playing", entry.Status);
            Assert.Equal(8, entry.Rating);
            Assert.True(entry.ContainsSpoilers);
        }
        using (var bDetail = await b.GetAsync("/games/9302"))
        {
            Assert.Equal(HttpStatusCode.OK, bDetail.StatusCode);
            Assert.DoesNotContain("Nice game", await bDetail.Content.ReadAsStringAsync());
        }
        await PostFormAsync(b, "/games/9302", "/games/9302?handler=Remove");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.Equal(1, await db.GameLibraryEntries.CountAsync(x => x.IgdbGameId == 9302));
        }
        await PostFormAsync(a, "/games/9302", "/games/9302?handler=Remove");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.Equal(0, await db.GameLibraryEntries.CountAsync(x => x.IgdbGameId == 9302));
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
    private sealed class FakeLibraryCatalog : ILibraryGameCatalog
    {
        public Task<IReadOnlyDictionary<int, LibraryGameMetadata>> GetByIdsAsync(
            IEnumerable<int> gameIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<int, LibraryGameMetadata> result =
                gameIds.Where(id => id == 7301).Distinct().ToDictionary(
                    id => id,
                    id => new LibraryGameMetadata(
                        id,
                        "Test game from IGDB",
                        "https://images.igdb.com/igdb/image/upload/t_cover_big/co123.jpg",
                        2024));
            return Task.FromResult(result);
        }
    }
    private sealed class FakeGameDetailCatalog : IGameDetailCatalog
    {
        public Task<GameDetailInfo?> GetAsync(int id, CancellationToken ct = default)
        {
            GameDetailInfo? result = id == 9302
                ? new GameDetailInfo(
                    id, "Example Game 9302",
                    "Great game <script>alert(1)</script>",
                    "https://images.igdb.com/igdb/image/upload/t_cover_big/co567.jpg",
                    2025,
                    ["Adventure"], ["PC"],
                    [new GameDetailCard(9303, "Suggested game", null, 2024)])
                : null;
            return Task.FromResult(result);
        }
    }

}
