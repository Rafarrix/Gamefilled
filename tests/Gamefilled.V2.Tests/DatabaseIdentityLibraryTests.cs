using System.Security.Claims;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Library;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class DatabaseIdentityLibraryTests
{
    [Fact]
    public async Task Identity_library_preserves_status_and_reviews_without_cross_account_writes()
    {
        // Real SQL is explicitly enabled in the dedicated ephemeral CI runner.
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__V2ModernDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connection));

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:V2ModernDatabase", connection);
        });

        GamefilledUser a;
        GamefilledUser b;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledUser>>();
            a = new GamefilledUser
            {
                Id = Guid.NewGuid(), UserName = "lib" + Guid.NewGuid().ToString("N")[..12],
                Email = Guid.NewGuid().ToString("N") + "@example.invalid", EmailConfirmed = true
            };
            b = new GamefilledUser
            {
                Id = Guid.NewGuid(), UserName = "lib" + Guid.NewGuid().ToString("N")[..12],
                Email = Guid.NewGuid().ToString("N") + "@example.invalid", EmailConfirmed = true
            };
            Assert.True((await users.CreateAsync(a, "fresh-test-passphrase-2026")).Succeeded);
            Assert.True((await users.CreateAsync(b, "fresh-test-passphrase-2026")).Succeeded);
        }

        var principalA = Viewer(a.Id);
        var principalB = Viewer(b.Id);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        const int gameId = 1901;

        using (var scope = factory.Services.CreateScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<GameLibraryService>();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => library.MyGamesAsync(anonymous));
            await Assert.ThrowsAsync<ArgumentException>(
                () => library.SaveStatusAsync(principalA, gameId, "finished"));

            var entry = await library.SaveStatusAsync(principalA, gameId, "  playing  ");
            Assert.Equal("playing", entry.Status);
            Assert.Equal(a.Id, entry.UserId);
            Assert.NotEmpty(entry.RowVersion);

            var reviewed = await library.SaveReviewAsync(principalA, gameId,
                9, "A spoiler-free review", false);
            Assert.Equal(9, reviewed.Rating);
            Assert.Equal("A spoiler-free review", reviewed.ReviewText);

            var updated = await library.SaveStatusAsync(principalA, gameId, "played");
            Assert.Equal("played", updated.Status);
            Assert.Equal(9, updated.Rating); // status update must not reset review

            Assert.Single(await library.MyGamesAsync(principalA));
            Assert.Empty(await library.MyGamesAsync(principalB));
            Assert.False(await library.RemoveAsync(principalB, gameId));
        }

        // Another user can independently track the same IGDB game, but
        // cannot change or remove the other user's entry.
        using (var scope = factory.Services.CreateScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<GameLibraryService>();
            var other = await library.SaveStatusAsync(principalB, gameId, "wishlist");
            Assert.Equal(b.Id, other.UserId);
            Assert.Equal("wishlist", other.Status);

            Assert.True(await library.RemoveAsync(principalB, gameId));
            Assert.Single(await library.MyGamesAsync(principalA));
            Assert.Empty(await library.MyGamesAsync(principalB));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            var rows = await db.GameLibraryEntries.AsNoTracking()
                .Where(x => x.IgdbGameId == gameId)
                .ToListAsync();
            Assert.Single(rows);
            Assert.Equal(a.Id, rows[0].UserId);
            Assert.Equal(9, rows[0].Rating);
            Assert.Equal("played", rows[0].Status);
        }
    }

    [Fact]
    public void Status_policy_accepts_only_supported_gamefilled_states()
    {
        Assert.Equal("backlog", GameLibraryStatus.Normalize(" Backlog "));
        Assert.Equal("played", GameLibraryStatus.Normalize("PLAYED"));
        Assert.Equal("playing", GameLibraryStatus.Normalize("playing"));
        Assert.Equal("wishlist", GameLibraryStatus.Normalize("wishlist"));
        Assert.Null(GameLibraryStatus.Normalize("owned"));
        Assert.Null(GameLibraryStatus.Normalize(""));
    }

    private static ClaimsPrincipal Viewer(Guid userId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))],
            IdentityConstants.ApplicationScheme));
}
