using Gamefilled.Application.Games;
using Microsoft.AspNetCore.Mvc;

namespace Gamefilled.Pages.games.lib;

/// <summary>
/// Base V2 para páginas de biblioteca que usam o motor central de descoberta.
/// Mantém compatibilidade com a view e com a classe base antiga enquanto a
/// migração é feita de forma gradual.
/// </summary>
public abstract class _FilteredGamesLibBase : _GamesLibBase
{
    private readonly IGameDiscoveryService _discoveryService;

    protected _FilteredGamesLibBase(
        IConfiguration configuration,
        ILogger logger,
        IHttpClientFactory httpClientFactory,
        IGameDiscoveryService discoveryService)
        : base(configuration, logger, httpClientFactory)
    {
        _discoveryService = discoveryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PlatformId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? GenreId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ReleaseYear { get; set; }

    [BindProperty(SupportsGet = true)]
    public double? MinimumRating { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReleaseStatus { get; set; }

    public IReadOnlyList<GameFilterOption> Platforms { get; private set; } = [];
    public IReadOnlyList<GameFilterOption> Genres { get; private set; } = [];
    public string? LoadError { get; private set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        PlatformId.HasValue ||
        GenreId.HasValue ||
        ReleaseYear.HasValue ||
        MinimumRating.HasValue ||
        !string.IsNullOrWhiteSpace(ReleaseStatus);

    /// <summary>
    /// Esconde o handler antigo da classe base para estas páginas já migrarem
    /// para o serviço V2. Os sorts de tempo continuam temporariamente na base antiga.
    /// </summary>
    public new async Task OnGetAsync()
    {
        Configure();

        var request = new GameDiscoveryRequest
        {
            PageNumber = PageNumber,
            PageSize = PageSize,
            Sort = SortKey,
            Direction = Dir,
            Search = Search,
            PlatformId = PlatformId,
            GenreId = GenreId,
            ReleaseYear = ReleaseYear,
            MinimumRating = MinimumRating,
            IsReleased = ParseReleaseStatus(ReleaseStatus)
        }.Normalize();

        // Reflete os valores normalizados no modelo e, consequentemente, nos links.
        PageNumber = request.PageNumber;
        PageSize = request.PageSize;
        Dir = request.Direction;
        Search = request.Search;
        PlatformId = request.PlatformId;
        GenreId = request.GenreId;
        ReleaseYear = request.ReleaseYear;
        MinimumRating = request.MinimumRating;
        ReleaseStatus = NormalizeReleaseStatus(ReleaseStatus);

        var cancellationToken = HttpContext.RequestAborted;

        var platformsTask = LoadOptionsSafelyAsync(
            () => _discoveryService.GetPlatformsAsync(cancellationToken),
            "platforms");

        var genresTask = LoadOptionsSafelyAsync(
            () => _discoveryService.GetGenresAsync(cancellationToken),
            "genres");

        try
        {
            var result = await _discoveryService.SearchAsync(request, cancellationToken);

            Games = result.Games
                .Select(game => new GameCard
                {
                    Id = game.Id,
                    Name = game.Name,
                    Slug = game.Slug,
                    CoverImageId = game.CoverImageId,
                    FirstReleaseDate = game.FirstReleaseDate,
                    Year = game.Year,
                    TotalRating = game.TotalRating,
                    TotalRatingCount = game.TotalRatingCount
                })
                .ToList();

            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            LastIgdbQuery = result.DiagnosticQuery;
            LastIgdbBody = null;
            LastIgdbStatus = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // O utilizador abandonou ou recarregou a página; não é um erro da aplicação.
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the V2 game library.");
            Games = [];
            TotalCount = 0;
            TotalPages = 0;
            LoadError = "Não foi possível carregar os jogos. Tenta novamente dentro de momentos.";
        }

        Platforms = await platformsTask;
        Genres = await genresTask;
    }

    private async Task<IReadOnlyList<GameFilterOption>> LoadOptionsSafelyAsync(
        Func<Task<IReadOnlyList<GameFilterOption>>> loader,
        string optionType)
    {
        try
        {
            return await loader();
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load IGDB {OptionType} metadata.",
                optionType);

            return [];
        }
    }

    private static bool? ParseReleaseStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "released" => true,
            "upcoming" => false,
            _ => null
        };

    private static string? NormalizeReleaseStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "released" => "released",
            "upcoming" => "upcoming",
            _ => null
        };
}
