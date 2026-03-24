namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Página da biblioteca ordenada por data de lançamento.
    /// </summary>
    public class ReleaseDateModel : _GamesLibBase
    {
        public ReleaseDateModel(IConfiguration cfg, ILogger<ReleaseDateModel> logger, IHttpClientFactory http)
            : base(cfg, logger, http) { }

        protected override void Configure()
        {
            SortKey = "release-date";
            PageTitle = "Release Date";
        }
    }
}