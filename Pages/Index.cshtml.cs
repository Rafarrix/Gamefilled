using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages
{
    /// <summary>
    /// Página inicial da aplicação.
    ///
    /// Responsabilidades:
    /// - detetar se o utilizador está autenticado
    /// - carregar blocos principais da home
    ///   - trending
    ///   - recent releases
    ///   - top rated
    /// - preparar helpers visuais para a view
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IgdbClient _igdb;

        public IndexModel(ILogger<IndexModel> logger, IgdbClient igdb)
        {
            _logger = logger;
            _igdb = igdb;
        }

        /// <summary>
        /// Jogos trending para a home.
        /// </summary>
        public List<IgdbGameDto> TrendingGames { get; private set; } = new();

        /// <summary>
        /// Jogos recentes para a home.
        /// </summary>
        public List<IgdbGameDto> RecentReleaseGames { get; private set; } = new();

        /// <summary>
        /// Jogos mais bem classificados para a home.
        /// </summary>
        public List<IgdbGameDto> TopRatedGames { get; private set; } = new();

        /// <summary>
        /// Estado de autenticação atual.
        /// </summary>
        public bool IsLoggedIn { get; private set; }

        /// <summary>
        /// Nome de apresentação do utilizador autenticado.
        /// </summary>
        public string? DisplayName { get; private set; }

        /// <summary>
        /// Username do utilizador autenticado.
        /// </summary>
        public string? Username { get; private set; }

        public async Task OnGetAsync(CancellationToken ct)
        {
            // Lê sessão atual.
            DisplayName = HttpContext.Session.GetString("displayName");
            Username = HttpContext.Session.GetString("username");
            IsLoggedIn = !string.IsNullOrWhiteSpace(Username);

            // Trending
            try
            {
                TrendingGames = await _igdb.GetTrendingGamesAsync(12, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar TrendingGames.");
                TrendingGames = new();
            }

            // Recent releases
            try
            {
                RecentReleaseGames = await _igdb.GetRecentReleasesAsync(12, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar RecentReleaseGames.");
                RecentReleaseGames = new();
            }

            // Top rated
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

        /// <summary>
        /// Constrói a URL da cover do jogo na IGDB.
        /// </summary>
        public string? BuildCoverUrl(string? imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId))
                return null;

            return $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
        }

        /// <summary>
        /// Extrai o ano a partir de UNIX timestamp.
        /// </summary>
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