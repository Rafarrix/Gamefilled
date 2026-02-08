namespace Gamefilled.Pages.games.lib
{
    public class TopRatedModel : _GamesLibBase
    {
        public TopRatedModel(IConfiguration cfg, ILogger<TopRatedModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "top-rated";
            PageTitle = "Top Rated";
        }
    }
}
