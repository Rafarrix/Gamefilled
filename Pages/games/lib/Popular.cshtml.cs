using Gamefilled.Application.Games;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por popularidade.
    /// </summary>
    public class PopularModel : _FilteredGamesLibBase
    {
        public PopularModel(
            IConfiguration cfg,
            ILogger<PopularModel> logger,
            IHttpClientFactory http,
            IGameDiscoveryService discoveryService)
            : base(cfg, logger, http, discoveryService) { }

        protected override void Configure()
        {
            SortKey = GameDiscoverySort.Popular;
            PageTitle = "Popularity";
        }
    }
}
