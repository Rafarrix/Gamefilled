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

        // The original Gamefilled records status and review actions for
        // profile timelines. Identity must preserve that active behavior.
        var activities = await database.Activities
            .Where(e => e.UserId == a && e.IgdbGameId == 11)
            .OrderBy(e => e.CreatedAtUtc).ToListAsync();
        Assert.Equal(3, activities.Count);
        Assert.Equal(2, activities.Count(e => e.Type == "game_status_changed"));
        Assert.Single(activities.Where(e => e.Type == "reviewed_game"));
        Assert.Contains(activities, e =>
            e.Type == "game_status_changed"
            && e.MetaJson is not null && e.MetaJson.Contains("playing"));
        Assert.Contains(activities, e =>
            e.Type == "reviewed_game"
            && e.MetaJson is not null && e.MetaJson.Contains("9"));

        Assert.Empty(await library.MyGamesAsync(Viewer(b)));
        Assert.False(await library.RemoveAsync(Viewer(b), 11));
        Assert.Single(await library.MyGamesAsync(Viewer(a)));
        Assert.True(await library.RemoveAsync(Viewer(a), 11));
        Assert.Empty(await library.MyGamesAsync(Viewer(a)));
    }

    [Fact]
    public async Task V2_follows_respect_private_profiles_and_mutual_blocks()
    {
        await using var database = CreateDatabase();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        database.Users.AddRange(
            new GamefilledAccount { Id = a, UserName = "alpha" },
            new GamefilledAccount { Id = b, UserName = "beta" },
            new GamefilledAccount { Id = c, UserName = "private", IsProfilePrivate = true });
        await database.SaveChangesAsync();

        var social = new GamefilledV2SocialService(database);
        Assert.False(await social.FollowAsync(Viewer(a), a));
        Assert.False(await social.FollowAsync(Viewer(a), c));
        Assert.True(await social.FollowAsync(Viewer(a), b));
        Assert.False(await social.FollowAsync(Viewer(a), b)); // idempotent
        Assert.True(await social.FollowAsync(Viewer(b), a));
        Assert.Equal(2, await database.Follows.CountAsync());

        Assert.Equal(2, await database.Activities.CountAsync(
            e => e.Type == "followed_user"));
        Assert.Equal(1, await database.Notifications.CountAsync(
            n => n.UserId == b && n.ActorUserId == a && n.Type == "new_follower"));
        Assert.Equal(1, await database.Notifications.CountAsync(
            n => n.UserId == a && n.ActorUserId == b && n.Type == "mutual_connection"));
        Assert.Equal(0, await database.Notifications.CountAsync(n => n.UserId == c));

        // Blocking either direction removes the connection in both directions.
        Assert.True(await social.BlockAsync(Viewer(b), a));
        Assert.Empty(await database.Follows.ToListAsync());
        Assert.False(await social.FollowAsync(Viewer(a), b));
        Assert.False(await social.FollowAsync(Viewer(b), a));
        Assert.False(await social.BlockAsync(Viewer(b), a));
        Assert.True(await social.UnblockAsync(Viewer(b), a));
        Assert.True(await social.FollowAsync(Viewer(a), b));
        Assert.False(await social.UnfollowAsync(Viewer(b), a));
        Assert.True(await social.UnfollowAsync(Viewer(a), b));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            social.FollowAsync(new ClaimsPrincipal(), b));
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
        var bell = await notifications.GetPreviewAsync(Viewer(a));
        Assert.Equal(2, bell.UnreadCount);
        Assert.Equal(2, bell.Items.Count);
        Assert.All(bell.Items, n => Assert.Equal("A player", n.ActorName));
        Assert.Null(await notifications.MarkReadAsync(Viewer(b), first));
        Assert.Equal(2, await notifications.UnreadCountAsync(Viewer(a)));

        Assert.Equal("/u/beta", await notifications.MarkReadAsync(Viewer(a), first));
        Assert.Equal(1, await notifications.UnreadCountAsync(Viewer(a)));
        Assert.Null(await notifications.MarkReadAsync(Viewer(a), external));
        Assert.Equal(0, await notifications.UnreadCountAsync(Viewer(a)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            notifications.MarkAllReadAsync(new ClaimsPrincipal()));
        await notifications.MarkAllReadAsync(Viewer(b));
        await notifications.MarkAllReadAsync(Viewer(a));
        Assert.All((await notifications.GetPreviewAsync(Viewer(a))).Items,
            n => Assert.True(n.IsRead));

        Assert.False(GamefilledV2NotificationService.IsSafeLocalPath("//evil.example"));
        Assert.False(GamefilledV2NotificationService.IsSafeLocalPath("/\\evil.example"));
        Assert.True(GamefilledV2NotificationService.IsSafeLocalPath("/games/11"));
    }
}
