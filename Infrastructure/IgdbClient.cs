using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Domain client for the IGDB game requests used by the current pages.
/// </summary>
public sealed class IgdbClient
{
    private readonly HttpClient _http;
    private readonly IgdbTokenProvider _tokenProvider;
    private readonly IgdbOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IgdbClient(
        HttpClient http,
        IgdbTokenProvider tokenProvider,
        IOptions<IgdbOptions> options)
    {
        _http = http;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    public async Task<List<IgdbGameDto>> SearchGamesAsync(
        string term,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length < 2)
            return [];

        limit = Math.Clamp(limit, 1, 50);

        var query = $"""
            fields id,name,slug,first_release_date,cover.image_id;
            search "{EscapeApicalypseString(term)}";
            where version_parent = null;
            limit {limit};
            """;

        return await QueryAsync<IgdbGameDto>("games", query, cancellationToken);
    }

    public async Task<IgdbGameDetailsDto?> GetGameDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var query = $"""
            fields
                id,name,slug,summary,first_release_date,
                cover.image_id,
                artworks.image_id,artworks.width,artworks.height,
                screenshots.image_id,screenshots.width,screenshots.height,
                videos.name,videos.video_id,
                genres.id,genres.name,genres.slug,
                platforms.id,platforms.name,platforms.slug,
                involved_companies.company.id,
                involved_companies.company.name,
                involved_companies.company.slug,
                involved_companies.company.logo.image_id,
                involved_companies.developer,
                involved_companies.publisher,
                involved_companies.porting,
                involved_companies.supporting,
                parent_game.id,parent_game.name,parent_game.slug,
                parent_game.first_release_date,parent_game.cover.image_id,
                dlcs.id,dlcs.name,dlcs.slug,dlcs.first_release_date,dlcs.cover.image_id,
                expansions.id,expansions.name,expansions.slug,
                expansions.first_release_date,expansions.cover.image_id,
                standalone_expansions.id,standalone_expansions.name,standalone_expansions.slug,
                standalone_expansions.first_release_date,standalone_expansions.cover.image_id,
                expanded_games.id,expanded_games.name,expanded_games.slug,
                expanded_games.first_release_date,expanded_games.cover.image_id,
                ports.id,ports.name,ports.slug,ports.first_release_date,ports.cover.image_id,
                remakes.id,remakes.name,remakes.slug,remakes.first_release_date,remakes.cover.image_id,
                remasters.id,remasters.name,remasters.slug,
                remasters.first_release_date,remasters.cover.image_id,
                similar_games.id,similar_games.name,similar_games.slug,
                similar_games.first_release_date,similar_games.cover.image_id,
                aggregated_rating,aggregated_rating_count,
                rating,rating_count,
                hypes,follows;
            where id = {id};
            limit 1;
            """;

        var games = await QueryAsync<IgdbGameDetailsDto>(
            "games",
            query,
            cancellationToken);

        return games.FirstOrDefault();
    }

    public async Task<List<IgdbGameDto>> GetTrendingGamesAsync(
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fiveYearsAgo = DateTimeOffset.UtcNow.AddYears(-5).ToUnixTimeSeconds();

        var query = $"""
            fields id,name,slug,first_release_date,cover.image_id;
            where cover != null
                & first_release_date != null
                & first_release_date >= {fiveYearsAgo}
                & first_release_date <= {now}
                & rating_count != null
                & rating_count > 10
                & version_parent = null;
            sort rating_count desc;
            limit {limit};
            """;

        return await QueryAsync<IgdbGameDto>("games", query, cancellationToken);
    }

    public async Task<List<IgdbGameDto>> GetRecentReleasesAsync(
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds();

        var query = $"""
            fields id,name,slug,first_release_date,cover.image_id;
            where cover != null
                & first_release_date != null
                & first_release_date >= {twoYearsAgo}
                & first_release_date <= {now}
                & version_parent = null;
            sort first_release_date desc;
            limit {limit};
            """;

        return await QueryAsync<IgdbGameDto>("games", query, cancellationToken);
    }

    public async Task<List<IgdbGameDto>> GetTopRatedGamesAsync(
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var query = $"""
            fields id,name,slug,first_release_date,cover.image_id;
            where cover != null
                & first_release_date != null
                & first_release_date <= {now}
                & aggregated_rating != null
                & aggregated_rating_count != null
                & aggregated_rating_count > 20
                & version_parent = null;
            sort aggregated_rating desc;
            limit {limit};
            """;

        return await QueryAsync<IgdbGameDto>("games", query, cancellationToken);
    }

    public async Task<List<IgdbGameDto>> GetGamesByIdsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var cleanIds = ids
            .Where(id => id > 0)
            .Distinct()
            .Take(500)
            .ToArray();

        if (cleanIds.Length == 0)
            return [];

        var query = $"""
            fields id,name,slug,first_release_date,cover.image_id;
            where id = ({string.Join(',', cleanIds)});
            limit {cleanIds.Length};
            """;

        return await QueryAsync<IgdbGameDto>("games", query, cancellationToken);
    }

    private async Task<List<T>> QueryAsync<T>(
        string endpoint,
        string query,
        CancellationToken cancellationToken)
    {
        using var request = await CreateIgdbRequestAsync(endpoint, query, cancellationToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"IGDB endpoint '{endpoint}' returned {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}). Body: {responseBody}",
                inner: null,
                response.StatusCode);
        }

        return JsonSerializer.Deserialize<List<T>>(responseBody, JsonOptions) ?? [];
    }

    private async Task<HttpRequestMessage> CreateIgdbRequestAsync(
        string endpoint,
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("IGDB ClientId is missing from configuration.");

        if (_http.BaseAddress is null)
            throw new InvalidOperationException("IgdbClient BaseAddress is not configured.");

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var requestUri = new Uri(_http.BaseAddress, endpoint.TrimStart('/'));

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("Client-ID", _options.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        return request;
    }

    private static string EscapeApicalypseString(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
