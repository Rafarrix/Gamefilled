using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gamefilled.Tests;

public sealed class PlatformSmokeTests : IClassFixture<GamefilledApplicationFactory>
{
    private readonly GamefilledApplicationFactory _factory;

    public PlatformSmokeTests(GamefilledApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_endpoint_returns_healthy_service_contract()
    {
        using var client = _factory.CreateSecureClient();

        using var response = await client.GetAsync("/health/live");
        var payload = await response.Content.ReadFromJsonAsync<LivenessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("healthy", payload.Status);
        Assert.Equal("gamefilled", payload.Service);
        Assert.NotEqual(default, payload.Timestamp);
    }

    [Fact]
    public async Task Responses_include_the_baseline_security_headers()
    {
        using var client = _factory.CreateSecureClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal("nosniff", HeaderValue(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", HeaderValue(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", HeaderValue(response, "Referrer-Policy"));
        Assert.Equal(
            "camera=(), microphone=(), geolocation=(), payment=()",
            HeaderValue(response, "Permissions-Policy"));
    }

    [Fact]
    public async Task Anonymous_user_cannot_open_notification_inbox()
    {
        using var client = _factory.CreateSecureClient();

        using var response = await client.GetAsync("/notifications");

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther,
            $"Expected a login redirect but received {(int)response.StatusCode}.");

        var location = response.Headers.Location?.OriginalString;
        Assert.False(string.IsNullOrWhiteSpace(location));
        Assert.Contains("/users/Login", location, StringComparison.OrdinalIgnoreCase);
    }

    private static string HeaderValue(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Missing header {name}.");
        return Assert.Single(values);
    }

    private sealed record LivenessResponse(string Status, string Service, DateTimeOffset Timestamp);
}
