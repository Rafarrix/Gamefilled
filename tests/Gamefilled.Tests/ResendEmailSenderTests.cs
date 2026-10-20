using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gamefilled.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gamefilled.Tests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task Sends_a_minimal_json_request_using_a_file_backed_key()
    {
        var path = Path.Combine(Path.GetTempPath(), "gamefilled-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(path, "re_test_not_a_real_secret\n");

            var handler = new RecordingHandler(HttpStatusCode.OK);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.resend.com/")
            };
            var sender = new ResendEmailSender(
                client, Options.Create(new ResendEmailOptions { ApiKeyFile = path }));

            await sender.SendAsync(
                "player@example.com", "Confirm your email",
                "A confirmation link will be placed here by the account system.");

            Assert.Equal(HttpMethod.Post, handler.Method);
            Assert.Equal("https://api.resend.com/emails", handler.RequestUrl);
            Assert.Equal("Bearer", handler.AuthorizationScheme);
            Assert.Equal("re_test_not_a_real_secret", handler.AuthorizationParameter);
            Assert.Contains("Gamefilled/2.0", handler.UserAgent);

            using var json = JsonDocument.Parse(Assert.IsType<string>(handler.JsonBody));
            var root = json.RootElement;
            Assert.Equal("Gamefilled <no-reply@gamefilled.pt>", root.GetProperty("from").GetString());
            Assert.Equal("player@example.com", root.GetProperty("to")[0].GetString());
            Assert.Equal("Confirm your email", root.GetProperty("subject").GetString());
            Assert.False(root.TryGetProperty("html", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_secret_fails_closed_without_calling_network()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var sender = new ResendEmailSender(
            client, Options.Create(new ResendEmailOptions { ApiKeyFile = "" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync("player@example.com", "Test", "A message"));

        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task Provider_failure_reports_only_status_without_response_body()
    {
        var path = Path.Combine(Path.GetTempPath(), "gamefilled-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(path, "re_test_not_a_real_secret");

            var handler = new RecordingHandler(HttpStatusCode.Forbidden);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.resend.com/")
            };
            var sender = new ResendEmailSender(
                client, Options.Create(new ResendEmailOptions { ApiKeyFile = path }));

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                sender.SendAsync("player@example.com", "Test", "A message"));

            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
            Assert.DoesNotContain("sensitive-response-body", ex.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RecordingHandler(HttpStatusCode code) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? RequestUrl { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? UserAgent { get; private set; }
        public string? JsonBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUrl = request.RequestUri?.ToString();
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            UserAgent = request.Headers.UserAgent.ToString();
            JsonBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(code)
            {
                Content = new StringContent("sensitive-response-body")
            };
        }
    }
}
