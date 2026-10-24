using Gamefilled.Infrastructure;
using Xunit;

namespace Gamefilled.Tests;

public sealed class PreviewSqlSecurityPolicyTests
{
    private const string Safe =
        "Server=sql,1433;Database=GamefilledFullPreview;User Id=gf_preview_app;" +
        "Password=TestOnly_Random_2026_NotReusable!42;Encrypt=True;" +
        "TrustServerCertificate=True";

    [Fact]
    public void App_must_only_use_non_admin_sql_login_in_the_preview_database()
    {
        PreviewSqlSecurityPolicy.RequireLeastPrivilegeConnection(Safe);
    }

    [Theory]
    [InlineData("sa", "GamefilledFullPreview", "sql,1433")]
    [InlineData("gf_preview_app", "Gamefilleddb", "sql,1433")]
    [InlineData("gf_preview_app", "GamefilledFullPreview", "web-01,1433")]
    [InlineData("gf_preview_app", "GamefilledV2Preview", "sql,1433")]
    [InlineData("root", "GamefilledFullPreview", "sql,1433")]
    public void Preview_rejects_admin_wrong_database_and_untrusted_server(
        string account, string database, string server)
    {
        var connection = "Server=" + server + ";Database=" + database
            + ";User Id=" + account + ";Password=TestOnly_BadTarget_2026!;"
            + "Encrypt=True;TrustServerCertificate=True";

        Assert.Throws<InvalidOperationException>(() =>
            PreviewSqlSecurityPolicy.RequireLeastPrivilegeConnection(connection));
    }

    [Fact]
    public void Preview_rejects_missing_connection_and_password()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PreviewSqlSecurityPolicy.RequireLeastPrivilegeConnection(""));

        Assert.Throws<InvalidOperationException>(() =>
            PreviewSqlSecurityPolicy.RequireLeastPrivilegeConnection(
                "Server=sql,1433;Database=GamefilledFullPreview;User Id=gf_preview_app"));
    }
}
