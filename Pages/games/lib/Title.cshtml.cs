namespace Gamefilled.Pages.games.lib
{
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
