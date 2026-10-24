using Microsoft.Data.SqlClient;

// One-shot provisioner, shipped ONLY in the setup-only container, never the
// web runtime image. Refuses connections outside isolated preview/CI SQL.
// Run after EF Core migrations, before switching preview web to a read/write
// account without sysadmin/DDL/database-owner privileges.
var adminConnectionString = Environment.GetEnvironmentVariable("GF_DB_ADMIN_CONNECTION");
var appPassword = Environment.GetEnvironmentVariable("GF_DB_APP_PASSWORD");
var target = Environment.GetEnvironmentVariable("GF_DB_TARGET") ?? "GamefilledFullPreview";
const string appLogin = "gf_preview_app";

if (string.IsNullOrWhiteSpace(adminConnectionString)
    || string.IsNullOrWhiteSpace(appPassword))
    throw new InvalidOperationException("Preview admin connection and app password are required.");

var cs = new SqlConnectionStringBuilder(adminConnectionString);
var ci = Environment.GetEnvironmentVariable("GF_PREVIEW_DB_CI") == "1";
var expectedServer = ci ? "127.0.0.1,14336" : "sql,1433";
var expectedTarget = ci ? "GamefilledPreviewPrivilegeCI" : "GamefilledFullPreview";
if (!string.Equals(cs.DataSource, expectedServer, StringComparison.OrdinalIgnoreCase)
    || !string.Equals(cs.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase)
    || !string.Equals(cs.UserID, "sa", StringComparison.OrdinalIgnoreCase)
    || !string.Equals(target, expectedTarget, StringComparison.OrdinalIgnoreCase)
    || cs.IntegratedSecurity)
    throw new InvalidOperationException("Refusing SQL provisioning outside the isolated preview target.");

if (appPassword.Length is < 24 or > 100
    || !appPassword.Any(char.IsUpper)
    || !appPassword.Any(char.IsLower)
    || !appPassword.Any(char.IsDigit)
    || !appPassword.Any(c => !char.IsLetterOrDigit(c)))
    throw new InvalidOperationException("Generate a unique random application SQL password (24–100 chars).");

await using var admin = new SqlConnection(cs.ConnectionString);
await admin.OpenAsync();

await using (var check = admin.CreateCommand())
{
    check.CommandText =
        "SELECT COUNT(1) FROM sys.databases WHERE name = @db AND state_desc = 'ONLINE'";
    check.Parameters.AddWithValue("@db", target);
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) != 1)
        throw new InvalidOperationException("Preview database must exist and be ONLINE after EF migrations.");
}

// The login identifier is intentionally a constant and cannot be supplied
// by untrusted input. QUOTENAME safely escapes the password literal.
await using (var login = admin.CreateCommand())
{
    login.CommandText = """
        DECLARE @quotedPassword nvarchar(210) = QUOTENAME(@password, '''');
        IF @quotedPassword IS NULL THROW 51001, 'Invalid application SQL password.', 1;
        DECLARE @statement nvarchar(max);
        IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'gf_preview_app')
            SET @statement = N'CREATE LOGIN [gf_preview_app] WITH PASSWORD = '
                + @quotedPassword + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
        ELSE
            SET @statement = N'ALTER LOGIN [gf_preview_app] WITH PASSWORD = '
                + @quotedPassword + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
        EXEC (@statement);
        IF IS_SRVROLEMEMBER('sysadmin', 'gf_preview_app') = 1
            THROW 51002, 'Application login must never be sysadmin.', 1;
        """;
    login.Parameters.AddWithValue("@password", appPassword);
    await login.ExecuteNonQueryAsync();
}

admin.ChangeDatabase(target);
await using (var user = admin.CreateCommand())
{
    user.CommandText = """
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'gf_preview_app')
            CREATE USER [gf_preview_app] FOR LOGIN [gf_preview_app];

        IF IS_ROLEMEMBER('db_owner', 'gf_preview_app') = 1
           OR IS_ROLEMEMBER('db_ddladmin', 'gf_preview_app') = 1
           THROW 51003, 'Application user must not own or alter schema.', 1;

        IF IS_ROLEMEMBER('db_datareader', 'gf_preview_app') <> 1
            ALTER ROLE [db_datareader] ADD MEMBER [gf_preview_app];
        IF IS_ROLEMEMBER('db_datawriter', 'gf_preview_app') <> 1
            ALTER ROLE [db_datawriter] ADD MEMBER [gf_preview_app];
        """;
    await user.ExecuteNonQueryAsync();
}

// Verify that the new credentials work from the same network as the web app,
// but cannot change database schema or access server administrator roles.
var runtimeCs = new SqlConnectionStringBuilder(cs.ConnectionString)
{
    InitialCatalog = target,
    UserID = appLogin,
    Password = appPassword,
    Pooling = false
};
await using (var runtime = new SqlConnection(runtimeCs.ConnectionString))
{
    await runtime.OpenAsync();
    await using var check = runtime.CreateCommand();
    check.CommandText = """
        SELECT
            ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONNECT'), 0),
            ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE'), 0),
            ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER'), 0),
            ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0),
            ISNULL(IS_MEMBER('db_owner'), 0),
            ISNULL(IS_MEMBER('db_datareader'), 0),
            ISNULL(IS_MEMBER('db_datawriter'), 0)
        """;
    await using var reader = await check.ExecuteReaderAsync();
    if (!await reader.ReadAsync()
        || reader.GetInt32(0) != 1
        || reader.GetInt32(1) != 0
        || reader.GetInt32(2) != 0
        || reader.GetInt32(3) != 0
        || reader.GetInt32(4) != 0
        || reader.GetInt32(5) != 1
        || reader.GetInt32(6) != 1)
        throw new InvalidOperationException("Application SQL privileges are outside the approved runtime scope.");
}

Console.WriteLine("PASS: isolated preview SQL runtime account can read/write, but is not admin or schema owner.");
