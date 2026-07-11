using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gamefilled.Tests;

public sealed class GamefilledApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly IReadOnlyDictionary<string, string?> TestConfiguration =
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=127.0.0.1,1;Database=GamefilledTests;User Id=sa;Password=NotUsed_TestOnly_123!;Encrypt=False;Connect Timeout=1",
            ["IGDB:ClientId"] = "integration-test-client",
            ["IGDB:ClientSecret"] = "integration-test-secret"
        };

    private readonly Dictionary<string, string?> _previousEnvironmentValues = new();

    public GamefilledApplicationFactory()
    {
        // Program validates essential settings immediately after CreateBuilder. Process-level
        // environment values must therefore exist before WebApplicationFactory starts the host.
        SetEnvironmentValue(
            "ConnectionStrings__DefaultConnection",
            TestConfiguration["ConnectionStrings:DefaultConnection"]);
        SetEnvironmentValue("IGDB__ClientId", TestConfiguration["IGDB:ClientId"]);
        SetEnvironmentValue("IGDB__ClientSecret", TestConfiguration["IGDB:ClientSecret"]);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(TestConfiguration);
        });
    }

    public HttpClient CreateSecureClient(bool allowAutoRedirect = false) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowAutoRedirect
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var pair in _previousEnvironmentValues)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }

    private void SetEnvironmentValue(string key, string? value)
    {
        _previousEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}
