namespace Gamefilled.Pages.games.lib
{
    public class PopularModel : _GamesLibBase
    {
        public PopularModel(IConfiguration cfg, ILogger<PopularModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "popular";
            PageTitle = "Popularity";
        }
    }
}
