using System.Text.Json.Serialization;
using Gamefilled.Application.Companies;

namespace Gamefilled.Infrastructure;

public sealed class CompanyService : ICompanyService
{
    private readonly IgdbApiClient _apiClient;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(
        IgdbApiClient apiClient,
        ILogger<CompanyService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<CompanyDirectoryResult> SearchAsync(
        string? search,
        int pageNumber = 1,
        int pageSize = 36,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 12, 60);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var offset = (pageNumber - 1) * pageSize;
        var searchLine = search is { Length: >= 2 }
            ? $"search \"{EscapeSearch(search)}\";"
            : string.Empty;
        var whereLine = "where name != null & slug != null;";

        var query = $"""
            fields id,name,slug,description,start_date,logo.image_id;
            {searchLine}
            {whereLine}
            sort name asc;
            limit {pageSize};
            offset {offset};
            """;

        var rows = await _apiClient.QueryAsync<List<IgdbCompanyDirectoryDto>>(
            "companies",
            query,
            cancellationToken);

        var countQuery = $"{searchLine}{whereLine}";
        var totalCount = await TryCountCompaniesAsync(
            countQuery,
            offset + rows.Count,
            cancellationToken);
        var totalPages = totalCount <= 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new CompanyDirectoryResult
        {
            Companies = rows
                .Where(row => row.Id > 0 && !string.IsNullOrWhiteSpace(row.Name))
                .Select(row => new CompanyDirectoryCard
                {
                    Id = row.Id,
                    Name = row.Name!.Trim(),
                    Slug = string.IsNullOrWhiteSpace(row.Slug)
                        ? row.Id.ToString()
                        : row.Slug.Trim(),
                    Description = row.Description?.Trim(),
                    LogoImageId = row.Logo?.ImageId,
                    StartDate = row.StartDate
                })
                .ToList(),
            Search = search,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<CompanyDetails?> GetDetailsAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            return null;

        var companyQuery = $"""
            fields
                id,name,slug,description,country,start_date,
                logo.image_id,
                parent.id,parent.name,parent.slug,
                status.name,
                company_size.name,
                websites.url,websites.trusted,websites.type.type;
            where id = {companyId};
            limit 1;
            """;

        var companyRows = await _apiClient.QueryAsync<List<IgdbCompanyDetailsDto>>(
            "companies",
            companyQuery,
            cancellationToken);

        var company = companyRows.FirstOrDefault();
        if (company is null || company.Id <= 0 || string.IsNullOrWhiteSpace(company.Name))
            return null;

        var involvementQuery = $"""
            fields game,developer,publisher,porting,supporting;
            where company = {companyId} & game != null;
            limit 500;
            """;

        var involvement = await _apiClient.QueryAsync<List<IgdbCompanyInvolvementDto>>(
            "involved_companies",
            involvementQuery,
            cancellationToken);

        var rolesByGame = involvement
            .Where(item => item.GameId > 0)
            .GroupBy(item => item.GameId)
            .ToDictionary(
                group => group.Key,
                group => new CompanyGameRoles(
                    group.Any(item => item.Developer),
                    group.Any(item => item.Publisher),
                    group.Any(item => item.Porting),
                    group.Any(item => item.Supporting)));

        var games = rolesByGame.Count == 0
            ? []
            : await LoadGamesAsync(rolesByGame, cancellationToken);

        return new CompanyDetails
        {
            Id = company.Id,
            Name = company.Name.Trim(),
            Slug = string.IsNullOrWhiteSpace(company.Slug)
                ? company.Id.ToString()
                : company.Slug.Trim(),
            Description = company.Description?.Trim(),
            CountryCode = company.Country,
            StartDate = company.StartDate,
            Status = company.Status?.Name,
            Size = company.CompanySize?.Name,
            LogoImageId = company.Logo?.ImageId,
            Parent = MapParent(company.Parent),
            Websites = (company.Websites ?? [])
                .Where(website => Uri.TryCreate(website.Url, UriKind.Absolute, out _))
                .Select(website => new CompanyWebsite(
                    website.Url!,
                    website.Type?.Type,
                    website.Trusted))
                .DistinctBy(website => website.Url, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Games = games
        };
    }

    private async Task<int> TryCountCompaniesAsync(
        string query,
        int fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.CountAsync("companies", query, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Unable to count IGDB company directory results.");
            return fallback;
        }
    }

    private async Task<IReadOnlyList<CompanyGameCard>> LoadGamesAsync(
        IReadOnlyDictionary<long, CompanyGameRoles> rolesByGame,
        CancellationToken cancellationToken)
    {
        var ids = rolesByGame.Keys
            .Where(id => id > 0)
            .Take(500)
            .ToArray();

        if (ids.Length == 0)
            return [];

        var gamesQuery = $"""
            fields id,name,slug,cover.image_id,first_release_date,total_rating;
            where id = ({string.Join(',', ids)})
                & name != null
                & slug != null
                & version_parent = null;
            sort first_release_date desc;
            limit {ids.Length};
            """;

        var games = await _apiClient.QueryAsync<List<IgdbCompanyGameDto>>(
            "games",
            gamesQuery,
            cancellationToken);

        return games
            .Where(game => game.Id > 0 && !string.IsNullOrWhiteSpace(game.Name))
            .Select(game =>
            {
                rolesByGame.TryGetValue(game.Id, out var roles);

                return new CompanyGameCard
                {
                    Id = game.Id,
                    Name = game.Name!.Trim(),
                    Slug = game.Slug?.Trim() ?? game.Id.ToString(),
                    CoverImageId = game.Cover?.ImageId,
                    FirstReleaseDate = game.FirstReleaseDate,
                    TotalRating = game.TotalRating,
                    IsDeveloper = roles?.Developer ?? false,
                    IsPublisher = roles?.Publisher ?? false,
                    IsPorting = roles?.Porting ?? false,
                    IsSupporting = roles?.Supporting ?? false
                };
            })
            .OrderByDescending(game => game.FirstReleaseDate ?? 0)
            .ThenBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CompanyReference? MapParent(IgdbCompanyReferenceDto? parent)
    {
        if (parent is null || parent.Id <= 0 || string.IsNullOrWhiteSpace(parent.Name))
            return null;

        return new CompanyReference(
            parent.Id,
            parent.Name.Trim(),
            string.IsNullOrWhiteSpace(parent.Slug) ? parent.Id.ToString() : parent.Slug.Trim());
    }

    private static string EscapeSearch(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private sealed record CompanyGameRoles(
        bool Developer,
        bool Publisher,
        bool Porting,
        bool Supporting);

    private sealed class IgdbCompanyDirectoryDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("start_date")]
        public long? StartDate { get; init; }

        [JsonPropertyName("logo")]
        public IgdbCompanyLogoDto? Logo { get; init; }
    }

    private sealed class IgdbCompanyDetailsDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("country")]
        public int? Country { get; init; }

        [JsonPropertyName("start_date")]
        public long? StartDate { get; init; }

        [JsonPropertyName("logo")]
        public IgdbCompanyLogoDto? Logo { get; init; }

        [JsonPropertyName("parent")]
        public IgdbCompanyReferenceDto? Parent { get; init; }

        [JsonPropertyName("status")]
        public IgdbNamedValueDto? Status { get; init; }

        [JsonPropertyName("company_size")]
        public IgdbNamedValueDto? CompanySize { get; init; }

        [JsonPropertyName("websites")]
        public List<IgdbCompanyWebsiteDto>? Websites { get; init; }
    }

    private sealed class IgdbCompanyReferenceDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }
    }

    private sealed class IgdbNamedValueDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class IgdbCompanyLogoDto
    {
        [JsonPropertyName("image_id")]
        public string? ImageId { get; init; }
    }

    private sealed class IgdbCompanyWebsiteDto
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("trusted")]
        public bool Trusted { get; init; }

        [JsonPropertyName("type")]
        public IgdbWebsiteTypeDto? Type { get; init; }
    }

    private sealed class IgdbWebsiteTypeDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }

    private sealed class IgdbCompanyInvolvementDto
    {
        [JsonPropertyName("game")]
        public long GameId { get; init; }

        [JsonPropertyName("developer")]
        public bool Developer { get; init; }

        [JsonPropertyName("publisher")]
        public bool Publisher { get; init; }

        [JsonPropertyName("porting")]
        public bool Porting { get; init; }

        [JsonPropertyName("supporting")]
        public bool Supporting { get; init; }
    }

    private sealed class IgdbCompanyGameDto
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("cover")]
        public IgdbCoverDto? Cover { get; init; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; init; }

        [JsonPropertyName("total_rating")]
        public double? TotalRating { get; init; }
    }
}
