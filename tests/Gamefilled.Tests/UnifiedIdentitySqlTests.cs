using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gamefilled.Tests;

/// <summary>
/// Runs only on GitHub's disposable SQL 2022 service, never a homelab/live DB.
/// Covers constraints that EF InMemory cannot actually enforce.
/// </summary>
public sealed class UnifiedIdentitySqlTests
{
    [Fact]
    public async Task Migrated_unified_schema_enforces_real_sql_ownership_and_constraints()
    {
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("GAMEFILLED_V2_DESIGN_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new SqlConnectionStringBuilder(connection);
        Assert.Equal("GamefilledV2IntegratedCI", parsed.InitialCatalog);
        Assert.StartsWith("127.0.0.1,", parsed.DataSource, StringComparison.Ordinal);

        var options = new DbContextOptionsBuilder<GamefilledV2DbContext>()
            .UseSqlServer(connection)
            .Options;

        await using var db = new GamefilledV2DbContext(options);
        Assert.True(await db.Database.CanConnectAsync());

        await using (var sql = new SqlConnection(connection))
        {
            await sql.OpenAsync();
            await using var command = new SqlCommand(
                "SELECT COUNT(*) FROM sys.tables WHERE name IN " +
                "('AspNetUsers','AspNetRoles','AspNetRoleClaims','AspNetUserClaims'," +
                "'AspNetUserLogins','AspNetUserRoles','AspNetUserTokens'," +
                "'GamefilledGameEntries','GamefilledFavoriteGames','GamefilledFollows'," +
                "'GamefilledActivities','GamefilledNotifications','GamefilledBlocks')", sql);
            Assert.Equal(13, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var emailA = "a-" + a.ToString("N") + "@example.invalid";
        var emailB = "b-" + b.ToString("N") + "@example.invalid";
        db.Users.AddRange(
            new GamefilledAccount
            {
                Id = a,
                UserName = "a_" + a.ToString("N")[..12],
                NormalizedUserName = "A_" + a.ToString("N")[..12].ToUpperInvariant(),
                Email = emailA,
                NormalizedEmail = emailA.ToUpperInvariant(),
                EmailConfirmed = true
            },
            new GamefilledAccount
            {
                Id = b,
                UserName = "b_" + b.ToString("N")[..12],
                NormalizedUserName = "B_" + b.ToString("N")[..12].ToUpperInvariant(),
                Email = emailB,
                NormalizedEmail = emailB.ToUpperInvariant(),
                EmailConfirmed = true
            });
        await db.SaveChangesAsync();

        db.GameEntries.Add(new GamefilledGameEntry
        {
            UserId = a, IgdbGameId = 777, Status = "playing", Rating = 9,
            ReviewText = "Saved through canonical GUID user foreign key"
        });
        db.Follows.Add(new GamefilledFollow { FollowerId = a, FollowingId = b });
        db.Blocks.Add(new GamefilledBlock { BlockerId = b, BlockedId = a });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.GameEntries.CountAsync(x => x.UserId == a));
        Assert.Equal(0, await db.GameEntries.CountAsync(x => x.UserId == b));
        Assert.True(await db.GameEntries.AnyAsync(x => x.UserId == a
            && x.IgdbGameId == 777 && x.Rating == 9));

        // Invalid state must be rejected by SQL CHECK, not just C# validation.
        db.GameEntries.Add(new GamefilledGameEntry
        {
            UserId = a, IgdbGameId = 778, Status = "steam-owned"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Reject duplicate game ownership even when two requests race.
        db.GameEntries.Add(new GamefilledGameEntry
        {
            UserId = a, IgdbGameId = 777, Status = "backlog"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Reject references to accounts absent from the Identity user table.
        db.GameEntries.Add(new GamefilledGameEntry
        {
            UserId = Guid.NewGuid(), IgdbGameId = 779, Status = "backlog"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Prevent a user following or blocking their own account.
        db.Follows.Add(new GamefilledFollow { FollowerId = a, FollowingId = a });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.Blocks.Add(new GamefilledBlock { BlockerId = a, BlockedId = a });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Existing rows are not lost after rejected mutations.
        Assert.Equal(1, await db.GameEntries.CountAsync(x => x.UserId == a));
        Assert.Equal(1, await db.Follows.CountAsync());
        Assert.Equal(1, await db.Blocks.CountAsync());
    }
}
