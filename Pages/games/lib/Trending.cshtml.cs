namespace Gamefilled.Pages.games.lib
{
    public class TrendingModel : _GamesLibBase
    {
        public TrendingModel(IConfiguration cfg, ILogger<TrendingModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "trending";
            PageTitle = "Trending";
        }
    }
}
