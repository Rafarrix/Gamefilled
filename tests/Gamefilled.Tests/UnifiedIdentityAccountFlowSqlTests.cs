using System.Text.RegularExpressions;
using Gamefilled.Application.Email;
using Gamefilled.Application.Identity;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gamefilled.Tests;

public sealed class UnifiedIdentityAccountFlowSqlTests
{
    [Fact]
    public async Task Real_sql_identity_registration_verification_login_policy_and_reset_work()
    {
        if (Environment.GetEnvironmentVariable("GAMEFILLED_V2_SQL_INTEGRATION") != "1")
            return;

        var connection = Environment.GetEnvironmentVariable("GAMEFILLED_V2_DESIGN_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection));

        var mail = new CapturingMail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Gamefilled:PublicOrigin"] = "https://preview.gamefilled.pt:8444"
            }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ITransactionalEmailSender>(mail);
        services.AddUnifiedGamefilledIdentity(connection!);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var accounts = scope.ServiceProvider
            .GetRequiredService<GamefilledIdentityAccountService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledAccount>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<GamefilledAccount>>();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var username = "user_" + unique;
        var email = "test_" + unique + "@example.invalid";
        const string initialPassword = "Strong-Passphrase-2026-A!";
        const string newPassword = "Another-Passphrase-2026-B!";

        Assert.False(await accounts.RegisterAsync(username, email, "short", true));
        Assert.False(await accounts.RegisterAsync(username, email, initialPassword, false));
        Assert.True(await accounts.RegisterAsync(username, email, initialPassword, true));

        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.Single(mail.Messages);

        // Password is valid, but signing in before confirmation must be denied.
        Assert.False((await signIn.CheckPasswordSignInAsync(
            user, initialPassword, lockoutOnFailure: true)).Succeeded);

        var confirmationCode = CodeFromEmail(mail.Messages[0].Text);
        Assert.True(await accounts.ConfirmEmailAsync(user.Id, confirmationCode));
        Assert.False(await accounts.ConfirmEmailAsync(user.Id, confirmationCode));
        Assert.True((await signIn.CheckPasswordSignInAsync(
            user, initialPassword, lockoutOnFailure: true)).Succeeded);

        // Duplicate registrations and password-reset requests use generic responses.
        Assert.True(await accounts.RegisterAsync(username, email, initialPassword, true));
        Assert.Single(mail.Messages);
        await accounts.RequestPasswordResetAsync("unknown-" + unique + "@example.invalid");
        Assert.Single(mail.Messages);

        await accounts.RequestPasswordResetAsync(email);
        Assert.Equal(2, mail.Messages.Count);
        var resetCode = CodeFromEmail(mail.Messages[1].Text);
        Assert.False(await accounts.ResetPasswordAsync(user.Id, "broken!", newPassword));
        Assert.True(await accounts.ResetPasswordAsync(user.Id, resetCode, newPassword));
        Assert.False(await accounts.ResetPasswordAsync(user.Id, resetCode, newPassword));

        Assert.False((await signIn.CheckPasswordSignInAsync(
            user, initialPassword, lockoutOnFailure: true)).Succeeded);
        Assert.True((await signIn.CheckPasswordSignInAsync(
            user, newPassword, lockoutOnFailure: true)).Succeeded);
    }

    [Fact]
    public void Security_links_reject_untrusted_origins_and_keep_local_paths()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Gamefilled:PublicOrigin"] = "https://preview.gamefilled.pt:8444"
            }).Build();
        var links = new GamefilledIdentityLinkFactory(config);
        var url = links.ConfirmationUrl(Guid.NewGuid(), "test-token");
        Assert.StartsWith("https://preview.gamefilled.pt:8444/users/confirm-email", url);
        Assert.Equal("test-token", GamefilledIdentityLinkFactory.DecodeConfirmationCode(
            CodeFromEmail(url)));

        foreach (var invalid in new[]
        {
            "http://gamefilled.pt",
            "https://attacker.example",
            "https://gamefilled.pt.attacker.example",
            "https://admin:pass@gamefilled.pt",
            "https://preview.gamefilled.pt:8444/unsafe"
        })
        {
            var untrusted = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Gamefilled:PublicOrigin"] = invalid })
                .Build();
            Assert.Throws<InvalidOperationException>(() =>
                new GamefilledIdentityLinkFactory(untrusted)
                    .ConfirmationUrl(Guid.NewGuid(), "test-token"));
        }
    }

    private static string CodeFromEmail(string text)
    {
        var match = Regex.Match(text, @"https://[^\s]+");
        Assert.True(match.Success);
        var code = QueryHelpers.ParseQuery(new Uri(match.Value).Query)["code"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));
        return code;
    }

    private sealed class CapturingMail : ITransactionalEmailSender
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
