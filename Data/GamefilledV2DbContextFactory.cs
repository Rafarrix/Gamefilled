using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gamefilled.Data;

/// <summary>
/// Design-time ONLY factory for generating/reviewing fresh V2 migrations.
/// This intentionally cannot read or fall back to the running site's credentials.
/// </summary>
public sealed class GamefilledV2DbContextFactory : IDesignTimeDbContextFactory<GamefilledV2DbContext>
{
    public GamefilledV2DbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("GAMEFILLED_V2_DESIGN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = "Server=127.0.0.1,1;Database=GamefilledV2DesignOnly;"
                + "User Id=sa;Password=DesignOnly_NotUsed_2026!;"
                + "Encrypt=False;Connect Timeout=1";
        }

        if (connection.Contains("Database=Gamefilleddb;", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to scaffold V2 Identity against live Gamefilleddb.");

        return new GamefilledV2DbContext(
            new DbContextOptionsBuilder<GamefilledV2DbContext>()
                .UseSqlServer(connection)
                .Options);
    }
}
