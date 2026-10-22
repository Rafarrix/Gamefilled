using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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

        var parsed = new SqlConnectionStringBuilder(connection);
        // Whitelist disposable, dedicated Identity database names. Explicitly
        // reject Gamefilleddb (dev), GamefilledFullPreview (legacy preview)
        // and ambiguous connection strings before any EF tooling can use them.
        // The migration command must never be able to select an arbitrary
        // database merely by prefixing its name with GamefilledV2.
        var permittedDatabases = new[]
        {
            "GamefilledV2DesignOnly",
            "GamefilledV2IntegratedCI",
            "GamefilledV2Integrated"
        };
        var permittedServers = new[]
        {
            "127.0.0.1,1",    // deliberately unreachable while scaffolding
            "127.0.0.1,14336", // disposable GitHub Actions SQL
            "sql,1433"       // isolated Docker Compose SQL on docker-01
        };
        if (!permittedDatabases.Contains(parsed.InitialCatalog, StringComparer.OrdinalIgnoreCase)
            || !permittedServers.Contains(parsed.DataSource, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Refusing V2 Identity migration: SQL server/database is not an isolated staging target.");

        return new GamefilledV2DbContext(
            new DbContextOptionsBuilder<GamefilledV2DbContext>()
                .UseSqlServer(connection)
                .Options);
    }
}
