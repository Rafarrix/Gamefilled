using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Cliente de baixo nível para executar queries APICalypse autenticadas.
/// Serviços de aplicação, como GameDiscoveryService, não precisam de conhecer
/// detalhes de OAuth, headers ou serialização HTTP.
/// </summary>
public sealed class IgdbApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IgdbTokenProvider _tokenProvider;
    private readonly IgdbOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IgdbApiClient(
        HttpClient httpClient,
        IgdbTokenProvider tokenProvider,
        IOptions<IgdbOptions> options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    public async Task<T> QueryAsync<T>(
        string endpoint,
        string query,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(endpoint, query, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"IGDB endpoint '{endpoint}' returned {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}). Body: {Truncate(responseBody, 500)}",
                inner: null,
                response.StatusCode);
        }

        var data = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
        return data ?? throw new InvalidOperationException(
            $"IGDB endpoint '{endpoint}' returned an empty or invalid JSON response.");
    }

    public async Task<int> CountAsync(
        string endpoint,
        string query,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync<IgdbCountResponse>(
            $"{endpoint.TrimEnd('/')}/count",
            query,
            cancellationToken);

        return Math.Max(0, result.Count);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        string endpoint,
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("IGDB ClientId is missing from configuration.");

        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException("IGDB HttpClient BaseAddress is not configured.");

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var requestUri = new Uri(_httpClient.BaseAddress, endpoint.TrimStart('/'));

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("Client-ID", _options.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        return request;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength
            ? value
            : value[..maxLength] + "…";

    private sealed class IgdbCountResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; init; }
    }
}
