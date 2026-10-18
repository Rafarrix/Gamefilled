using Gamefilled.Application.Games;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por data de lançamento.
    /// </summary>
    public class ReleaseDateModel : _FilteredGamesLibBase
    {
        public ReleaseDateModel(
            IConfiguration cfg,
            ILogger<ReleaseDateModel> logger,
            IHttpClientFactory http,
            IGameDiscoveryService discoveryService)
            : base(cfg, logger, http, discoveryService) { }

        protected override void Configure()
        {
            SortKey = GameDiscoverySort.ReleaseDate;
            PageTitle = "Release Date";
        }
    }
}
