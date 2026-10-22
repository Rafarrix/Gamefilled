using System.Security.Claims;
using Gamefilled.Application.Identity;
using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gamefilled.Tests;

public sealed class UnifiedIdentityOwnershipTests
{
    private static GamefilledV2DbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<GamefilledV2DbContext>()
            .UseInMemoryDatabase("gf-v2-" + Guid.NewGuid().ToString("N")).Options;
        return new GamefilledV2DbContext(options);
    }

    private static ClaimsPrincipal Viewer(Guid id) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.ToString("D"))],
            IdentityConstants.ApplicationScheme));

    [Fact]
    public async Task V2_library_keeps_ratings_and_reviews_owned_by_a_single_identity()
    {
        await using var database = CreateDatabase();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        database.Users.AddRange(
            new GamefilledAccount { Id = a, UserName = "alpha", Email = "alpha@example.invalid" },
            new GamefilledAccount { Id = b, UserName = "beta", Email = "beta@example.invalid" });
        await database.SaveChangesAsync();

        var library = new GamefilledV2LibraryService(database);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            library.MyGamesAsync(new ClaimsPrincipal()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            library.SaveStatusAsync(Viewer(Guid.Empty), 11, "played"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            library.SaveStatusAsync(Viewer(a), 11, "steam-owned"));

        await library.SaveStatusAsync(Viewer(a), 11, "Playing");
        await library.SaveReviewAsync(Viewer(a), 11, 9, "  Excellent game  ", false);
        await library.SaveStatusAsync(Viewer(a), 11, "played");

        var aGames = await library.MyGamesAsync(Viewer(a));
        Assert.Single(aGames);
        Assert.Equal("played", aGames[0].Status);
        Assert.Equal(9, aGames[0].Rating);
        Assert.Equal("Excellent game", aGames[0].ReviewText);

        Assert.Empty(await library.MyGamesAsync(Viewer(b)));
        Assert.False(await library.RemoveAsync(Viewer(b), 11));
        Assert.Single(await library.MyGamesAsync(Viewer(a)));
        Assert.True(await library.RemoveAsync(Viewer(a), 11));
        Assert.Empty(await library.MyGamesAsync(Viewer(a)));
    }

    [Fact]
    public async Task V2_notifications_reject_other_users_and_external_redirects()
    {
        await using var database = CreateDatabase();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var first = Guid.NewGuid();
        var external = Guid.NewGuid();
        database.Users.AddRange(
            new GamefilledAccount { Id = a, UserName = "alpha" },
            new GamefilledAccount { Id = b, UserName = "beta" });
        database.Notifications.AddRange(
            new GamefilledNotification
            {
                Id = first, UserId = a, Type = "new_follower", TargetUrl = "/u/beta"
            },
            new GamefilledNotification
            {
                Id = external, UserId = a, Type = "new_follower",
                TargetUrl = "https://attacker.example/"
            });
        await database.SaveChangesAsync();

        var notifications = new GamefilledV2NotificationService(database);
        Assert.Equal(2, await notifications.UnreadCountAsync(Viewer(a)));
        Assert.Equal(0, await notifications.UnreadCountAsync(Viewer(b)));
        Assert.Null(await notifications.MarkReadAsync(Viewer(b), first));
        Assert.Equal(2, await notifications.UnreadCountAsync(Viewer(a)));

        Assert.Equal("/u/beta", await notifications.MarkReadAsync(Viewer(a), first));
        Assert.Equal(1, await notifications.UnreadCountAsync(Viewer(a)));
        Assert.Null(await notifications.MarkReadAsync(Viewer(a), external));
        Assert.Equal(0, await notifications.UnreadCountAsync(Viewer(a)));

        Assert.False(GamefilledV2NotificationService.IsSafeLocalPath("//evil.example"));
        Assert.False(GamefilledV2NotificationService.IsSafeLocalPath("/\\evil.example"));
        Assert.True(GamefilledV2NotificationService.IsSafeLocalPath("/games/11"));
    }
}
