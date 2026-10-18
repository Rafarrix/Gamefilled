using Gamefilled.Application.Games;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada alfabeticamente por título.
    /// </summary>
    public class TitleModel : _FilteredGamesLibBase
    {
        public TitleModel(
            IConfiguration cfg,
            ILogger<TitleModel> logger,
            IHttpClientFactory http,
            IGameDiscoveryService discoveryService)
            : base(cfg, logger, http, discoveryService) { }

        protected override void Configure()
        {
            SortKey = GameDiscoverySort.Title;
            PageTitle = "Game Title";
        }
    }
}
