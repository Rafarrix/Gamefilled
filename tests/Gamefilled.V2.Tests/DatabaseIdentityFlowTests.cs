using System.Net;
using System.Text.RegularExpressions;
using Gamefilled.Application.Email;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gamefilled.V2.Tests;

/// <summary>
/// Executes only when ConnectionStrings__V2ModernDatabase is explicitly set
/// to the temporary SQL Server used by the separate CI job.
/// </summary>
public sealed class DatabaseIdentityFlowTests
{
    [Fact]
    public async Task Registration_requires_email_confirmation_before_sign_in()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__V2ModernDatabase");
        if (string.IsNullOrWhiteSpace(connection))
            return; // The separate SQL integration job provides this.

        var mail = new CapturingSender();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:V2ModernDatabase", connection);
            host.UseSetting("Gamefilled:PublicOrigin", "https://dev.gamefilled.pt");
            host.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransactionalEmailSender>();
                services.AddSingleton<ITransactionalEmailSender>(mail);
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://dev.gamefilled.pt"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamefilledIdentityDbContext>();
            Assert.True(await db.Database.CanConnectAsync());
            Assert.Contains("20261021192111_InitialIdentity", await db.Database.GetAppliedMigrationsAsync());
        }

        var username = "p" + Guid.NewGuid().ToString("N")[..12];
        var email = $"{username}@example.invalid";
        const string password = "a-long-test-passphrase-2026";

        // The anti-CSRF token must be included on every state-changing form.
        using (var registerPage = await client.GetAsync("/account/register"))
        {
            Assert.Equal(HttpStatusCode.OK, registerPage.StatusCode);
            var csrf = GetCsrf(await registerPage.Content.ReadAsStringAsync());
            using var register = await client.PostAsync("/account/register", Form(
                ("Username", username), ("Email", email), ("Password", password),
                ("AcceptTerms", "true"), ("__RequestVerificationToken", csrf)));
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        }

        Assert.Equal(email, mail.Recipient);
        Assert.NotNull(mail.Message);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<GamefilledUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(await users.IsEmailConfirmedAsync(user!));
        }

        // Correct password is not enough until the address has been verified.
        await AssertLoginAsync(client, email, password, shouldSucceed: false);

        var link = Regex.Match(mail.Message!, @"https://dev\.gamefilled\.pt/account/confirm\?[^\s]+").Value;
        Assert.NotEmpty(link);
        var confirmation = new Uri(link);
        var parameters = confirmation.Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));

        using (var confirmPage = await client.GetAsync(confirmation.PathAndQuery))
        {
            Assert.Equal(HttpStatusCode.OK, confirmPage.StatusCode);
            var csrf = GetCsrf(await confirmPage.Content.ReadAsStringAsync());
            using var confirmed = await client.PostAsync("/account/confirm", Form(
                ("UserId", parameters["userId"]), ("Code", parameters["code"]),
                ("__RequestVerificationToken", csrf)));
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.Contains("Your address has been confirmed",
                await confirmed.Content.ReadAsStringAsync());
        }

        // A second use of the same confirmation token must not succeed.
        using (var confirmPage = await client.GetAsync(confirmation.PathAndQuery))
        {
            var csrf = GetCsrf(await confirmPage.Content.ReadAsStringAsync());
            using var again = await client.PostAsync("/account/confirm", Form(
                ("UserId", parameters["userId"]), ("Code", parameters["code"]),
                ("__RequestVerificationToken", csrf)));
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Contains("invalid or expired", await again.Content.ReadAsStringAsync());
        }

        await AssertLoginAsync(client, email, password, shouldSucceed: true);

        using var profile = await client.GetAsync("/identity/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    private static async Task AssertLoginAsync(
        HttpClient client, string email, string password, bool shouldSucceed)
    {
        using var page = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var csrf = GetCsrf(await page.Content.ReadAsStringAsync());
        using var login = await client.PostAsync("/account/login", Form(
            ("Email", email), ("Password", password),
            ("__RequestVerificationToken", csrf)));
        Assert.Equal(shouldSucceed ? HttpStatusCode.Redirect : HttpStatusCode.OK,
            login.StatusCode);
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)));

    private static string GetCsrf(string html)
    {
        var match = Regex.Match(html,
            "<input(?=[^>]*name=\"__RequestVerificationToken\")(?=[^>]*value=\"([^\"]+)\")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(match.Success, "Razor form must render an antiforgery token.");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class CapturingSender : ITransactionalEmailSender
    {
        public string? Recipient { get; private set; }
        public string? Message { get; private set; }

        public Task SendAsync(string recipient, string subject, string text,
            string? html = null, CancellationToken cancellationToken = default)
        {
            Recipient = recipient;
            Message = text;
            return Task.CompletedTask;
        }
    }
}
