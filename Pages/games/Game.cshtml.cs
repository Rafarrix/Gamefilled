using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gamefilled.Pages.games
{
    /// <summary>
    /// PageModel da página de detalhe de jogo.
    ///
    /// Responsabilidades:
    /// - pedir detalhe de jogo ao endpoint interno /api/igdbgame
    /// - pedir time-to-beat ao endpoint interno /api/igdbttb
    /// - preparar URLs de background e cover
    /// </summary>
    public class GameModel : PageModel
    {
        /// <summary>
        /// Factory de HttpClient.
        /// </summary>
        private readonly IHttpClientFactory _http;

        /// <summary>
        /// Logger da página.
        /// </summary>
        private readonly ILogger<GameModel> _logger;

        public GameModel(IHttpClientFactory http, ILogger<GameModel> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>
        /// Detalhes do jogo carregado.
        /// </summary>
        public IgdbGameDetailsDto? Game { get; private set; }

        /// <summary>
        /// Tempo médio de jogo (normally) em segundos.
        /// </summary>
        public int? TtbNormallySeconds { get; private set; }

        /// <summary>
        /// Tempo para completar (completely) em segundos.
        /// </summary>
        public int? TtbCompletelySeconds { get; private set; }

        /// <summary>
        /// Tempo em modo rápido (hastily) em segundos.
        /// </summary>
        public int? TtbHastilySeconds { get; private set; }

        /// <summary>
        /// URL da imagem de fundo.
        /// </summary>
        public string? BgUrl { get; private set; }

        /// <summary>
        /// URL da cover principal.
        /// </summary>
        public string? CoverUrl { get; private set; }

        /// <summary>
        /// Handler GET da página de detalhe.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        {
            if (id <= 0) return NotFound();

            try
            {
                var client = _http.CreateClient();

                // Helper para construir URL absoluta do endpoint interno se necessário.
                string MakeAbs(string rel)
                {
                    if (client.BaseAddress != null) return rel;

                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    return $"{baseUrl}{rel}";
                }

                // 1) Pede os detalhes do jogo ao endpoint interno.
                var gameRes = await client.GetAsync(MakeAbs($"/api/igdbgame?id={id}"), ct);
                var gameJson = await gameRes.Content.ReadAsStringAsync(ct);

                if (!gameRes.IsSuccessStatusCode)
                {
                    _logger.LogWarning("/api/igdbgame falhou: {Status} | Body: {Body}", gameRes.StatusCode, gameJson);
                    return StatusCode(500);
                }

                var gameData = JsonSerializer.Deserialize<List<IgdbGameDetailsDto>>(
                    gameJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                );

                Game = gameData?.FirstOrDefault();

                if (Game == null)
                    return NotFound();

                // 2) Pede time to beat (best effort).
                var ttbRes = await client.GetAsync(MakeAbs($"/api/igdbttb?id={id}"), ct);

                if (ttbRes.IsSuccessStatusCode)
                {
                    var ttbJson = await ttbRes.Content.ReadAsStringAsync(ct);
                    ParseTtb(ttbJson);
                }

                // Escolha da imagem de fundo:
                // 1. artwork
                // 2. screenshot
                // 3. cover
                var bgId =
                    Game.Artworks?.FirstOrDefault()?.ImageId ??
                    Game.Screenshots?.FirstOrDefault()?.ImageId ??
                    Game.Cover?.ImageId;

                BgUrl = BuildIgdbImage(bgId, "t_1080p_2x", "webp");
                CoverUrl = BuildIgdbImage(Game.Cover?.ImageId, "t_cover_big", "jpg");

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar jogo {Id}", id);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Extrai normalmente/completely/hastily do JSON do endpoint TTB.
        /// </summary>
        private void ParseTtb(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

                var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Object) return;

                if (first.TryGetProperty("normally", out var n) && n.ValueKind == JsonValueKind.Number)
                    TtbNormallySeconds = n.GetInt32();

                if (first.TryGetProperty("completely", out var c) && c.ValueKind == JsonValueKind.Number)
                    TtbCompletelySeconds = c.GetInt32();

                if (first.TryGetProperty("hastily", out var h) && h.ValueKind == JsonValueKind.Number)
                    TtbHastilySeconds = h.GetInt32();
            }
            catch
            {
                // Best effort: se falhar o parse, a página continua sem TTB.
            }
        }

        /// <summary>
        /// Constrói URL completa de imagem da IGDB.
        /// </summary>
        private static string? BuildIgdbImage(string? imageId, string size, string ext)
            => string.IsNullOrWhiteSpace(imageId)
                ? null
                : $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.{ext}";
    }
}