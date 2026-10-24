using Microsoft.Data.SqlClient;

// Security boundary: audit never writes, never logs account identifiers,
// hashes, email addresses, connection strings, or arbitrary database data.
// The only accepted targets are isolated Gamefilled SQL databases.
var connection = Environment.GetEnvironmentVariable("GF_AUDIT_CONNECTION")
    ?? throw new InvalidOperationException("GF_AUDIT_CONNECTION is required.");

SqlConnectionStringBuilder cs;
try
{
    cs = new SqlConnectionStringBuilder(connection);
}
catch (ArgumentException)
{
    throw new InvalidOperationException("Audit connection is invalid.");
}

var isCi = Environment.GetEnvironmentVariable("GF_PREVIEW_DB_CI") == "1";
var expectedHost = isCi ? "127.0.0.1,14336" : "sql,1433";
var expectedDatabase = isCi ? "GamefilledPreviewPrivilegeCI" : "GamefilledFullPreview";

if (!string.Equals(cs.DataSource, expectedHost, StringComparison.OrdinalIgnoreCase)
    || !string.Equals(cs.InitialCatalog, expectedDatabase, StringComparison.OrdinalIgnoreCase)
    || !string.Equals(cs.UserID, "gf_preview_app", StringComparison.OrdinalIgnoreCase)
    || cs.IntegratedSecurity || string.IsNullOrWhiteSpace(cs.Password))
{
    throw new InvalidOperationException(
        "Audit refused: target must be the isolated preview DB and non-admin account.");
}

var metrics = new (string Id, string Sql, bool Blocker)[]
{
    ("accounts", "SELECT COUNT_BIG(*) FROM dbo.Users", false),
    ("library_entries", "SELECT COUNT_BIG(*) FROM dbo.UserGameEntries", false),
    ("favorites", "SELECT COUNT_BIG(*) FROM dbo.UserFavoriteGames", false),
    ("follows", "SELECT COUNT_BIG(*) FROM dbo.Follows", false),
    ("activities", "SELECT COUNT_BIG(*) FROM dbo.UserActivities", false),
    ("notifications", "SELECT COUNT_BIG(*) FROM dbo.UserNotifications", false),

    // New Identity accepts only 3-30 ASCII [a-zA-Z0-9_] usernames.
    // Never trim/rename these silently or map two accounts to one.
    ("invalid_usernames", """
        SELECT COUNT_BIG(*) FROM dbo.Users
        WHERE Username IS NULL OR LEN(LTRIM(RTRIM(Username))) NOT BETWEEN 3 AND 30
           OR LTRIM(RTRIM(Username)) COLLATE Latin1_General_100_BIN2 LIKE '%[^A-Za-z0-9_]%'
        """, true),
    ("duplicate_normalized_usernames", """
        SELECT COALESCE(SUM(g.qty - 1), 0) FROM
          (SELECT COUNT_BIG(*) AS qty FROM dbo.Users
           WHERE Username IS NOT NULL
           GROUP BY UPPER(LTRIM(RTRIM(Username))) HAVING COUNT_BIG(*) > 1) AS g
        """, true),
    ("missing_or_malformed_emails", """
        SELECT COUNT_BIG(*) FROM dbo.Users
        WHERE Email IS NULL OR LEN(LTRIM(RTRIM(Email))) > 254
           OR LTRIM(RTRIM(Email)) NOT LIKE '%_@_%._%'
        """, true),
    ("duplicate_normalized_emails", """
        SELECT COALESCE(SUM(g.qty - 1), 0) FROM
          (SELECT COUNT_BIG(*) AS qty FROM dbo.Users
           WHERE Email IS NOT NULL
           GROUP BY UPPER(LTRIM(RTRIM(Email))) HAVING COUNT_BIG(*) > 1) AS g
        """, true),
    ("nonstandard_account_roles", """
        SELECT COUNT_BIG(*) FROM dbo.Users
        WHERE Role IS NULL OR Role <> N'User'
        """, true),
    ("unexpected_password_hash_format", """
        SELECT COUNT_BIG(*) FROM dbo.Users
        WHERE PasswordHash IS NULL
           OR PasswordHash NOT LIKE '$2[aby]$%'
        """, true),
    // Old MetaJson is nvarchar(max); V2 currently only allows 4000.
    ("activity_metadata_over_4000", """
        SELECT COUNT_BIG(*) FROM dbo.UserActivities
        WHERE LEN(MetaJson) > 4000
        """, true),
    // Old favorites have CreatedAt; staged V2 favorites currently do not.
    ("favorite_timestamps_to_preserve", """
        SELECT COUNT_BIG(*) FROM dbo.UserFavoriteGames
        """, true),
    ("external_notification_links", """
        SELECT COUNT_BIG(*) FROM dbo.UserNotifications
        WHERE TargetUrl IS NOT NULL
          AND (TargetUrl NOT LIKE '/%' OR TargetUrl LIKE '//%'
               OR TargetUrl LIKE '/\\%')
        """, true)
};

await using var db = new SqlConnection(cs.ConnectionString);
await db.OpenAsync();

var blockers = 0;
Console.WriteLine("Gamefilled legacy-to-Identity readiness audit (aggregate counts only)");
Console.WriteLine("Source: isolated preview SQL, READ ONLY");
foreach (var metric in metrics)
{
    await using var cmd = db.CreateCommand();
    cmd.CommandText = metric.Sql;
    cmd.CommandTimeout = 30;
    var scalar = await cmd.ExecuteScalarAsync();
    var count = Convert.ToInt64(scalar);
    if (count < 0)
        throw new InvalidOperationException("Invalid aggregate count returned.");

    // These are aggregate counts, never any user-specific values.
    Console.WriteLine(metric.Id + "=" + count);
    if (metric.Blocker && count > 0) blockers++;
}

Console.WriteLine(blockers == 0
    ? "RESULT: No audit blockers detected. NOT proof that cutover is safe."
    : "RESULT: BLOCKED. Resolve all incompatibilities before any account import.");

if (Environment.GetEnvironmentVariable("GF_AUDIT_ENFORCE") == "1" && blockers > 0)
    Environment.ExitCode = 3;
