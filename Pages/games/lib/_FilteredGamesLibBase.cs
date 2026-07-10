using Gamefilled.Application.Games;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gamefilled.Pages.games.lib;

/// <summary>
/// V2 base for library pages using the centralized discovery engine.
/// During the gradual migration it works as a page filter and stops the
/// inherited legacy handler after loading data through the V2 service.
/// </summary>
public abstract class _FilteredGamesLibBase : _GamesLibBase, IAsyncPageFilter
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

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) =>
        Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        await LoadV2Async();

        // Stops the inherited legacy handler so the page performs only one
        // discovery request and exposes a single effective page-handler path.
        context.Result = Page();
    }

    private async Task LoadV2Async()
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
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the V2 game library.");
            Games = [];
            TotalCount = 0;
            TotalPages = 0;
            LoadError = "The games could not be loaded. Please try again in a moment.";
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
