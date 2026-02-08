namespace Gamefilled.Pages.games.lib
{
    public class AvgFinishTimeModel : _GamesLibBase
    {
        public AvgFinishTimeModel(IConfiguration cfg, ILogger<AvgFinishTimeModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "avg-finish";
            PageTitle = "Avg. Finish Time";
        }
    }
}
