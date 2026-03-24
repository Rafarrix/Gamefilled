namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada alfabeticamente por título.
    /// </summary>
    public class TitleModel : _GamesLibBase
    {
        public TitleModel(IConfiguration cfg, ILogger<TitleModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "title";
            PageTitle = "Game Title";
        }
    }
}