using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using Gamefilled.Application.Email;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Uses an API key supplied through a mounted secret file, never through a
/// committed config file or an HTTP request. No email content is logged.
/// </summary>
public sealed class ResendEmailSender : ITransactionalEmailSender
{
    private readonly HttpClient _http;
    private readonly ResendEmailOptions _options;

    public ResendEmailSender(HttpClient http, IOptions<ResendEmailOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task SendAsync(
        string recipient,
        string subject,
        string text,
        string? html = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient)
            || recipient.Length > 254
            || !MailAddress.TryCreate(recipient, out var address)
            || !string.Equals(address.Address, recipient, StringComparison.Ordinal))
        {
            throw new ArgumentException("A single valid recipient is required.", nameof(recipient));
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200)
            throw new ArgumentException("A subject of 1-200 characters is required.", nameof(subject));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Plain-text email content is required.", nameof(text));

        if (string.IsNullOrWhiteSpace(_options.ApiKeyFile)
            || !Path.IsPathFullyQualified(_options.ApiKeyFile))
        {
            throw new InvalidOperationException("The Resend API key file is not configured.");
        }

        // Read on demand. An unreadable/missing key must fail closed.
        // The file is mounted outside the source tree and container image.
        var key = (await File.ReadAllTextAsync(_options.ApiKeyFile, cancellationToken)).Trim();
        if (!key.StartsWith("re_", StringComparison.Ordinal))
            throw new InvalidOperationException("The Resend API key file is invalid.");

        var payload = new Dictionary<string, object>
        {
            ["from"] = "Gamefilled <no-reply@gamefilled.pt>",
            ["to"] = new[] { recipient },
            ["subject"] = subject,
            ["text"] = text
        };

        if (!string.IsNullOrWhiteSpace(html))
            payload["html"] = html;

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.UserAgent.ParseAdd("Gamefilled/2.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Do not print API responses, tokens, recipient details or request bodies.
            throw new HttpRequestException(
                $"Transactional email provider returned HTTP {(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}

public sealed class ResendEmailOptions
{
    public string ApiKeyFile { get; set; } = string.Empty;
}
