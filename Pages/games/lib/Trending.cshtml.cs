using Gamefilled.Application.Games;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por tendência/popularidade recente.
    /// </summary>
    public class TrendingModel : _FilteredGamesLibBase
    {
        public TrendingModel(
            IConfiguration cfg,
            ILogger<TrendingModel> logger,
            IHttpClientFactory http,
            IGameDiscoveryService discoveryService)
            : base(cfg, logger, http, discoveryService) { }

        protected override void Configure()
        {
            SortKey = GameDiscoverySort.Trending;
            PageTitle = "Trending";
        }
    }
}
