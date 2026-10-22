using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gamefilled.Tests;

/// <summary>
/// Mapping checks against the future integrated V2 schema. The running preview
/// still uses the legacy DbContext until every authenticated route is moved.
/// </summary>
public sealed class UnifiedIdentityDomainTests
{
    private static GamefilledV2DbContext CreateModel()
    {
        var options = new DbContextOptionsBuilder<GamefilledV2DbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=ModelOnly;User Id=sa;Password=Model_Only_123!;Encrypt=False")
            .Options;
        return new GamefilledV2DbContext(options);
    }

    [Fact]
    public void Future_account_model_uses_one_guid_identity_and_unique_email()
    {
        using var db = CreateModel();
        var user = db.Model.FindEntityType(typeof(GamefilledAccount));
        Assert.NotNull(user);
        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.Equal(typeof(Guid), user.FindProperty(nameof(GamefilledAccount.Id))!.ClrType);
        Assert.True(user.GetIndexes().Any(i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(GamefilledAccount.NormalizedEmail)])));

        Assert.NotNull(db.Model.FindEntityType(typeof(IdentityRole<Guid>)));
        Assert.NotNull(db.Model.FindEntityType(typeof(IdentityUserClaim<Guid>)));
    }

    [Fact]
    public void Every_new_user_owned_entity_uses_guid_fk_into_one_identity_table()
    {
        using var db = CreateModel();
        foreach (var entityType in new[]
        {
            typeof(GamefilledGameEntry),
            typeof(GamefilledFavoriteGame),
            typeof(GamefilledFollow),
            typeof(GamefilledActivity),
            typeof(GamefilledNotification)
        })
        {
            var entity = db.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
            Assert.All(entity.GetForeignKeys(), fk =>
            {
                Assert.Equal(typeof(GamefilledAccount), fk.PrincipalEntityType.ClrType);
                Assert.All(fk.Properties, column => Assert.Equal(typeof(Guid),
                    Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType));
            });
        }
    }

    [Fact]
    public void Library_and_social_constraints_reject_duplicate_or_invalid_relations()
    {
        using var db = CreateModel();
        var entry = db.Model.FindEntityType(typeof(GamefilledGameEntry))!;
        Assert.True(entry.GetIndexes().Any(i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(GamefilledGameEntry.UserId), nameof(GamefilledGameEntry.IgdbGameId)])));
        Assert.Contains(entry.GetCheckConstraints(), c =>
            c.Name == "CK_GF_GameEntries_Rating");
        Assert.Contains(entry.GetCheckConstraints(), c =>
            c.Name == "CK_GF_GameEntries_Status");

        var follow = db.Model.FindEntityType(typeof(GamefilledFollow))!;
        Assert.Contains(follow.GetCheckConstraints(), c =>
            c.Name == "CK_GF_Follows_NoSelfFollow");
        Assert.True(follow.GetIndexes().Any(i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(GamefilledFollow.FollowerId), nameof(GamefilledFollow.FollowingId)])));

        var favorites = db.Model.FindEntityType(typeof(GamefilledFavoriteGame))!;
        Assert.True(favorites.GetIndexes().Any(i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(GamefilledFavoriteGame.UserId), nameof(GamefilledFavoriteGame.SortOrder)])));
    }
}
