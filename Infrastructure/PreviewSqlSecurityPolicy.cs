using Microsoft.Data.SqlClient;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Fail closed if the PRIVATE Gamefilled preview is accidentally configured
/// with an admin SQL login or points to a different database/SQL host.
/// This is activated only for ASPNETCORE_ENVIRONMENT=Preview.
/// </summary>
public static class PreviewSqlSecurityPolicy
{
    public static void RequireLeastPrivilegeConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Preview database connection is missing.");

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Preview database connection is invalid.");
        }

        if (!string.Equals(builder.DataSource, "sql,1433", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(builder.InitialCatalog, "GamefilledFullPreview",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(builder.UserID, "gf_preview_app",
                StringComparison.OrdinalIgnoreCase)
            || builder.IntegratedSecurity
            || string.IsNullOrWhiteSpace(builder.Password))
        {
            // Never include the connection string (contains secrets) in errors.
            throw new InvalidOperationException(
                "Preview must use only gf_preview_app on GamefilledFullPreview at sql,1433.");
        }
    }
}
