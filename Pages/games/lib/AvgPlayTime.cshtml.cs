namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por tempo médio de jogo.
    /// </summary>
    public class AvgPlayTimeModel : _GamesLibBase
    {
        public AvgPlayTimeModel(IConfiguration cfg, ILogger<AvgPlayTimeModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "avg-play";
            PageTitle = "Avg. Play Time";
        }
    }
}