using System.Net;
using Gamefilled.V2.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class IdentityAccountPagesTests : IClassFixture<ModernIdentityApplicationFactory>
{
    private readonly ModernIdentityApplicationFactory _factory;
    public IdentityAccountPagesTests(ModernIdentityApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/account/register")]
    [InlineData("/account/login")]
    [InlineData("/account/resend")]
    [InlineData("/account/confirm?userId=00000000-0000-0000-0000-000000000000&code=invalid")]
    public async Task Account_pages_render_anonymously_without_a_database_query(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/account/register")]
    [InlineData("/account/login")]
    [InlineData("/account/resend")]
    [InlineData("/account/confirm")]
    public async Task Account_mutation_without_antiforgery_token_is_rejected(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.PostAsync(path,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "test@example.com",
                ["Password"] = "never-use-in-production",
                ["Username"] = "tester",
                ["AcceptTerms"] = "true"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Confirmation_links_have_approved_https_origin_and_url_safe_tokens()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gamefilled:PublicOrigin"] = "https://dev.gamefilled.pt"
            }).Build();

        var factory = new AccountLinkFactory(configuration);
        var url = factory.ConfirmationUrl(Guid.Empty, "a+b/==");
        Assert.StartsWith("https://dev.gamefilled.pt/account/confirm?", url);
        Assert.DoesNotContain("+", url);
        Assert.Contains("code=", url);

        var code = new Uri(url).Query.Split("code=", 2)[1];
        Assert.Equal("a+b/==", AccountLinkFactory.DecodeConfirmationCode(code));
    }

    [Theory]
    [InlineData("http://gamefilled.pt")]
    [InlineData("https://gamefilled.pt.attacker.example")]
    [InlineData("https://gamefilled.pt/?host=bad")]
    [InlineData("https://user:pass@gamefilled.pt")]
    [InlineData("")]
    public void Confirmation_links_reject_untrusted_origins(string origin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gamefilled:PublicOrigin"] = origin
            }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            new AccountLinkFactory(configuration).ConfirmationUrl(Guid.NewGuid(), "token"));
    }
}
