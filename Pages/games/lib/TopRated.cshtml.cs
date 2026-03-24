namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por rating.
    /// </summary>
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