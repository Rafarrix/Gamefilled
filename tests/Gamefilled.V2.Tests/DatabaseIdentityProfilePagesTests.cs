using System.Net;
using System.Text.RegularExpressions;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Library;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class DatabaseIdentityProfilePagesTests
{
    [Fact]
    public async Task Private_profile_reads_and_writes_only_its_authenticated_owner()
    {
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__V2ModernDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connection));

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:V2ModernDatabase", connection);
        });

        const string password = "valid-profile-test-passphrase";
        var a = new GamefilledUser
        {
            Id = Guid.NewGuid(),
            UserName = "profilea" + Guid.NewGuid().ToString("N")[..10],
            Email = Guid.NewGuid().ToString("N") + "@example.invalid",
            EmailConfirmed = true,
            DisplayName = "Private Alice"
        };
        var b = new GamefilledUser
        {
            Id = Guid.NewGuid(),
            UserName = "profileb" + Guid.NewGuid().ToString("N")[..10],
            Email = Guid.NewGuid().ToString("N") + "@example.invalid",
            EmailConfirmed = true,
            DisplayName = "Private Bob"
        };

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledUser>>();
            Assert.True((await users.CreateAsync(a, password)).Succeeded);
            Assert.True((await users.CreateAsync(b, password)).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            db.GameLibraryEntries.AddRange(
                new GameLibraryEntry { UserId = a.Id, IgdbGameId = 18101, Status = GameLibraryStatus.Played },
                new GameLibraryEntry { UserId = a.Id, IgdbGameId = 18102, Status = GameLibraryStatus.Backlog },
                new GameLibraryEntry { UserId = b.Id, IgdbGameId = 18103, Status = GameLibraryStatus.Wishlist });
            await db.SaveChangesAsync();
        }

        using var anonymous = Client(factory);
        using var denied = await anonymous.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);

        using var clientA = Client(factory);
        using var clientB = Client(factory);
        await SignInAsync(clientA, a.Email!, password);
        await SignInAsync(clientB, b.Email!, password);

        using (var ownPage = await clientA.GetAsync("/profile"))
        {
            Assert.Equal(HttpStatusCode.OK, ownPage.StatusCode);
            Assert.Contains("no-store", ownPage.Headers.CacheControl?.ToString());
            var html = await ownPage.Content.ReadAsStringAsync();
            Assert.Contains("Private Alice", html);
            Assert.Contains("2</strong>", html);
            Assert.DoesNotContain("Private Bob", html);
            Assert.DoesNotContain(b.Email!, html);
        }
        using (var otherPage = await clientB.GetAsync("/profile"))
        {
            var html = await otherPage.Content.ReadAsStringAsync();
            Assert.Contains("Private Bob", html);
            Assert.Contains("1</strong>", html);
            Assert.DoesNotContain("Private Alice", html);
            Assert.DoesNotContain(a.Email!, html);
        }

        // A real authenticated cookie alone does not bypass Razor antiforgery.
        using (var csrf = await clientA.PostAsync("/profile",
            new FormUrlEncodedContent(
                [new KeyValuePair<string, string>("DisplayName", "No token")])))
        {
            Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        }

        // Invalid display names render an error and never update persisted data.
        using (var invalid = await PostProfileAsync(clientA, new string('x', 101)))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.Equal("Private Alice", (await db.Users.SingleAsync(u => u.Id == a.Id)).DisplayName);
        }

        using (var changed = await PostProfileAsync(clientA, "<script>alert(1)</script>"))
        {
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        }
        using (var page = await clientA.GetAsync("/profile"))
        {
            var html = await page.Content.ReadAsStringAsync();
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
            Assert.DoesNotContain("<script>alert(1)</script>", html);
        }

        // Supplying an arbitrary target userId in a form cannot affect another account.
        using (var changedB = await PostProfileAsync(clientB, "Bob updated", a.Id.ToString()))
        {
            Assert.Equal(HttpStatusCode.Redirect, changedB.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.Equal("<script>alert(1)</script>",
                (await db.Users.SingleAsync(u => u.Id == a.Id)).DisplayName);
            Assert.Equal("Bob updated",
                (await db.Users.SingleAsync(u => u.Id == b.Id)).DisplayName);
            Assert.Equal(2, await db.GameLibraryEntries.CountAsync(e => e.UserId == a.Id));
            Assert.Equal(1, await db.GameLibraryEntries.CountAsync(e => e.UserId == b.Id));
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
        using var page = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var token = ExtractToken(await page.Content.ReadAsStringAsync());
        using var response = await client.PostAsync("/account/login",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("Email", email),
                new KeyValuePair<string, string>("Password", password),
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            ]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var identity = await client.GetAsync("/identity/me");
        Assert.Equal(HttpStatusCode.OK, identity.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostProfileAsync(
        HttpClient client, string name, string? forgedUserId = null)
    {
        using var page = await client.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var token = ExtractToken(await page.Content.ReadAsStringAsync());
        var fields = new List<KeyValuePair<string, string>>
        {
            new("DisplayName", name),
            new("__RequestVerificationToken", token)
        };
        if (forgedUserId is not null)
            fields.Add(new("UserId", forgedUserId));
        return await client.PostAsync("/profile", new FormUrlEncodedContent(fields));
    }

    private static string ExtractToken(string html)
    {
        var token = Regex.Match(html,
            "<input(?=[^>]*name=\"__RequestVerificationToken\")(?=[^>]*value=\"([^\"]+)\")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "Razor POST form must include antiforgery token.");
        return System.Net.WebUtility.HtmlDecode(token.Groups[1].Value);
    }
}
