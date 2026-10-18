using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Obtém e reutiliza o token OAuth da Twitch usado pela IGDB.
/// A instância é registada como singleton para o cache ser partilhado por toda a aplicação.
/// </summary>
public sealed class IgdbTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IgdbOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _token;
    private DateTimeOffset _tokenExpiresAtUtc;

    public IgdbTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<IgdbOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (HasValidToken())
            return _token!;

        await _refreshLock.WaitAsync(ct);

        try
        {
            // Outro pedido pode ter atualizado o token enquanto esperávamos pelo lock.
            if (HasValidToken())
                return _token!;

            if (string.IsNullOrWhiteSpace(_options.ClientId) ||
                string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                throw new InvalidOperationException(
                    "IGDB ClientId/ClientSecret are missing from configuration.");
            }

            var client = _httpClientFactory.CreateClient("TwitchAuth");

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "client_credentials"
            });

            using var response = await client.PostAsync(
                "https://id.twitch.tv/oauth2/token",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Twitch OAuth returned {(int)response.StatusCode} " +
                    $"({response.ReasonPhrase}). Body: {Truncate(responseBody, 300)}",
                    inner: null,
                    response.StatusCode);
            }

            var data = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(
                cancellationToken: ct);

            if (data is null || string.IsNullOrWhiteSpace(data.AccessToken))
                throw new InvalidOperationException("Twitch returned an invalid OAuth token response.");

            _token = data.AccessToken;

            // Nunca considera o token válido para além da expiração real.
            var safeLifetimeSeconds = Math.Max(1, data.ExpiresIn - 60);
            _tokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(safeLifetimeSeconds);

            return _token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool HasValidToken() =>
        !string.IsNullOrWhiteSpace(_token) &&
        DateTimeOffset.UtcNow < _tokenExpiresAtUtc;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength] + "…";
}
