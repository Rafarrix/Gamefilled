using System.Security.Claims;
using Gamefilled.Application.Email;
using Gamefilled.Application.Identity;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gamefilled.Tests;

/// <summary>
/// Against disposable SQL only, asserts that account settings are owned by
/// a single cookie Identity and email is not changed before confirmation.
/// </summary>
public sealed class UnifiedIdentitySettingsSqlTests
{
    [Fact]
    public async Task Real_sql_changes_password_revokes_stamp_and_verifies_new_email_before_update()
    {
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("GAMEFILLED_V2_DESIGN_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));

        var mail = new FakeMail();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Gamefilled:PublicOrigin"] = "https://preview.gamefilled.pt:8444"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ITransactionalEmailSender>(mail);
        services.AddUnifiedGamefilledIdentity(connection!);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledAccount>>();
        var profile = scope.ServiceProvider.GetRequiredService<GamefilledIdentityProfileService>();
        var settings = scope.ServiceProvider.GetRequiredService<GamefilledIdentityAccountSettingsService>();

        var accountId = Guid.NewGuid();
        var firstEmail = "owner-" + accountId.ToString("N") + "@example.invalid";
        var nextEmail = "changed-" + accountId.ToString("N") + "@example.invalid";
        const string oldPassword = "Strong-Passphrase-2026-A!";
        const string newPassword = "Another-Passphrase-2026-B!";

        var account = new GamefilledAccount
        {
            Id = accountId,
            UserName = "profile_" + accountId.ToString("N")[..10],
            Email = firstEmail,
            EmailConfirmed = true
        };
        Assert.True((await users.CreateAsync(account, oldPassword)).Succeeded);
        var owner = Authenticated(accountId);
        var stranger = Authenticated(Guid.NewGuid());

        Assert.Null(await profile.MyProfileAsync(stranger));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => profile.MyProfileAsync(new ClaimsPrincipal()));

        Assert.False(await profile.UpdateMyProfileAsync(
            stranger, "intruder", null, null, null, false));
        Assert.False(await profile.UpdateMyProfileAsync(
            owner, "Owner", "This is my profile", "javascript:alert(1)", null, false));
        Assert.True(await profile.UpdateMyProfileAsync(
            owner, "My display name", "I collect RPGs.", "/lib/imgs/defaultavatar.png",
            "https://images.igdb.com/example.png", true));

        var details = await profile.MyProfileAsync(owner);
        Assert.NotNull(details);
        Assert.Equal("My display name", details.DisplayName);
        Assert.True(details.IsPrivate);
        Assert.Equal("/lib/imgs/defaultavatar.png", details.AvatarUrl);

        var earlierStamp = (await users.FindByIdAsync(accountId.ToString("D")))!.SecurityStamp;
        Assert.False(await settings.ChangeMyPasswordAsync(
            stranger, oldPassword, newPassword));
        Assert.False(await settings.ChangeMyPasswordAsync(
            owner, "wrong", newPassword));
        Assert.True(await settings.ChangeMyPasswordAsync(
            owner, oldPassword, newPassword));
        var latest = (await users.FindByIdAsync(accountId.ToString("D")))!;
        Assert.NotEqual(earlierStamp, latest.SecurityStamp);
        Assert.False(await users.CheckPasswordAsync(latest, oldPassword));
        Assert.True(await users.CheckPasswordAsync(latest, newPassword));

        Assert.False(await settings.RequestEmailChangeAsync(
            owner, "wrong", nextEmail));
        Assert.Empty(mail.Messages);
        Assert.True(await settings.RequestEmailChangeAsync(
            owner, newPassword, nextEmail));
        Assert.Single(mail.Messages);
        Assert.Equal(nextEmail, mail.Messages[0].Recipient);

        // The original address remains authoritative before new inbox consent.
        Assert.Equal(firstEmail,
            (await users.FindByIdAsync(accountId.ToString("D")))!.Email);

        var linkText = System.Text.RegularExpressions.Regex.Match(
            mail.Messages[0].Text, @"https://[^\s]+").Value;
        Assert.False(string.IsNullOrWhiteSpace(linkText));
        var link = new Uri(linkText);
        var query = QueryHelpers.ParseQuery(link.Query);
        var code = query["code"].ToString();
        var encodedEmail = query["email"].ToString();
        Assert.Equal(nextEmail, GamefilledIdentityLinkFactory.DecodeTargetEmail(encodedEmail));

        Assert.False(await settings.ConfirmEmailChangeAsync(accountId, encodedEmail, "bad-token"));
        Assert.True(await settings.ConfirmEmailChangeAsync(accountId, encodedEmail, code));
        Assert.Equal(nextEmail,
            (await users.FindByIdAsync(accountId.ToString("D")))!.Email);
        Assert.False(await settings.ConfirmEmailChangeAsync(accountId, encodedEmail, code));
    }

    private static ClaimsPrincipal Authenticated(Guid userId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))],
            IdentityConstants.ApplicationScheme));

    private sealed class FakeMail : ITransactionalEmailSender
    {
        public List<(string Recipient, string Text)> Messages { get; } = [];

        public Task SendAsync(string recipient, string subject, string text,
            string? html = null, CancellationToken cancellationToken = default)
        {
            Messages.Add((recipient, text));
            return Task.CompletedTask;
        }
    }
}
