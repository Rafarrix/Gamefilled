using System.Net;
using Gamefilled.Application.Security;
using Xunit;

namespace Gamefilled.Tests;

public sealed class LoginSecurityTests
{
    [Fact]
    public void Wrong_credentials_from_new_sessions_share_an_identifier_lock()
    {
        using var guard = new LoginAttemptGuard();

        for (var i = 0; i < 4; i++)
        {
            guard.RecordFailure("TEST@EXAMPLE.COM");
            Assert.False(guard.IsBlocked("test@example.com", out _));
        }

        guard.RecordFailure("test@example.com");
        Assert.True(guard.IsBlocked(" TEST@example.com ", out var remaining));
        Assert.InRange(remaining, 1, 5);
    }

    [Fact]
    public void Successful_authentication_resets_prior_failures()
    {
        using var guard = new LoginAttemptGuard();

        for (var i = 0; i < 5; i++)
            guard.RecordFailure("player");
        Assert.True(guard.IsBlocked("player", out _));

        guard.RecordSuccess("player");
        Assert.False(guard.IsBlocked("player", out _));
    }

    [Fact]
    public async Task Login_page_rate_limits_repeated_requests()
    {
        using var factory = new GamefilledApplicationFactory();
        using var client = factory.CreateSecureClient();

        for (var i = 0; i < 20; i++)
        {
            using var response = await client.GetAsync("/users/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var throttled = await client.GetAsync("/users/Login");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
