using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IgdbClient _igdb;

        public IndexModel(ILogger<IndexModel> logger, IgdbClient igdb)
        {
            _logger = logger;
            _igdb = igdb;
        }

        public List<IgdbGameDto> TrendingGames { get; private set; } = new();
        public List<IgdbGameDto> RecentReleaseGames { get; private set; } = new();
        public List<IgdbGameDto> TopRatedGames { get; private set; } = new();

        public bool IsLoggedIn { get; private set; }
        public string? DisplayName { get; private set; }
        public string? Username { get; private set; }

        public async Task OnGetAsync(CancellationToken ct)
        {
            DisplayName = HttpContext.Session.GetString("displayName");
            Username = HttpContext.Session.GetString("username");
            IsLoggedIn = !string.IsNullOrWhiteSpace(Username);

            try
            {
                TrendingGames = await _igdb.GetTrendingGamesAsync(12, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar TrendingGames.");
                TrendingGames = new();
            }

            try
            {
                RecentReleaseGames = await _igdb.GetRecentReleasesAsync(12, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar RecentReleaseGames.");
                RecentReleaseGames = new();
            }

            try
            {
                TopRatedGames = await _igdb.GetTopRatedGamesAsync(12, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar TopRatedGames.");
                TopRatedGames = new();
            }
        }

        public string? BuildCoverUrl(string? imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId))
                return null;

            return $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
        }

        public int? GetYear(long? unix)
        {
            if (unix == null) return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year;
            }
            catch
            {
                return null;
            }
        }
    }
}