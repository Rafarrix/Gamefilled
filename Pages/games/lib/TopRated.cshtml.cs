using Gamefilled.Application.Games;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por rating.
    /// </summary>
    public class TopRatedModel : _FilteredGamesLibBase
    {
        public TopRatedModel(
            IConfiguration cfg,
            ILogger<TopRatedModel> logger,
            IHttpClientFactory http,
            IGameDiscoveryService discoveryService)
            : base(cfg, logger, http, discoveryService) { }

        protected override void Configure()
        {
            SortKey = GameDiscoverySort.TopRated;
            PageTitle = "Top Rated";
        }
    }
}
