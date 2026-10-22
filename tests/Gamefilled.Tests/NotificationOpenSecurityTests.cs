using System.Net;
using Gamefilled.Pages;
using Xunit;

namespace Gamefilled.Tests;

public sealed class NotificationOpenSecurityTests
{
    [Fact]
    public void Opening_a_notification_has_no_GET_mutation_handler()
    {
        // GETs must remain safe for browser prefetch, link previews and crawlers.
        var unsafeHandler = typeof(NotificationsModel).GetMethod("OnGetOpenAsync");
        Assert.Null(unsafeHandler);

        Assert.NotNull(typeof(NotificationsModel).GetMethod("OnPostOpenAsync"));
    }

    [Fact]
    public async Task Opening_a_notification_by_POST_without_antiforgery_is_rejected()
    {
        using var factory = new GamefilledApplicationFactory();
        using var client = factory.CreateSecureClient();

        using var form = new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("id", "123") });

        using var response = await client.PostAsync("/notifications?handler=Open", form);

        // Razor Pages automatically validates the antiforgery token before
        // invoking the handler, even for a signed-in cookie.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
