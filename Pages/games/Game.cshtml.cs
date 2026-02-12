using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gamefilled.Pages.games
{
    public class GameModel : PageModel
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<GameModel> _logger;

        public GameModel(IHttpClientFactory http, ILogger<GameModel> logger)
        {
            _http = http;
            _logger = logger;
        }

        public IgdbGameDetailsDto? Game { get; private set; }

        // IGDB TTB (normalmente em segundos)
        public int? TtbNormallySeconds { get; private set; }    // average
        public int? TtbCompletelySeconds { get; private set; }  // to finish
        public int? TtbHastilySeconds { get; private set; }     // rush

        public string? BgUrl { get; private set; }
        public string? CoverUrl { get; private set; }

        public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        {
            if (id <= 0) return NotFound();

            try
            {
                var client = _http.CreateClient();

                string MakeAbs(string rel)
                {
                    if (client.BaseAddress != null) return rel;
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    return $"{baseUrl}{rel}";
                }

                // 1) game details (via endpoint interno)
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
                if (Game == null) return NotFound();

                // 2) time-to-beat (best effort)
                var ttbRes = await client.GetAsync(MakeAbs($"/api/igdbttb?id={id}"), ct);
                if (ttbRes.IsSuccessStatusCode)
                {
                    var ttbJson = await ttbRes.Content.ReadAsStringAsync(ct);
                    ParseTtb(ttbJson);
                }

                // Bg prefer: artwork -> screenshot -> cover
                var bgId =
                    Game.Artworks?.FirstOrDefault()?.ImageId ??
                    Game.Screenshots?.FirstOrDefault()?.ImageId ??
                    Game.Cover?.ImageId;

                // Backloggd-like: 1080p 2x + webp
                BgUrl = BuildIgdbImage(bgId, "t_1080p_2x", "webp");

                // cover grande (podes trocar p/ t_cover_big_2x se quiseres)
                CoverUrl = BuildIgdbImage(Game.Cover?.ImageId, "t_cover_big", "jpg");

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar jogo {Id}", id);
                return StatusCode(500);
            }
        }

        private void ParseTtb(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

                var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Object) return;

                if (first.TryGetProperty("normally", out var n) && n.ValueKind == JsonValueKind.Number) TtbNormallySeconds = n.GetInt32();
                if (first.TryGetProperty("completely", out var c) && c.ValueKind == JsonValueKind.Number) TtbCompletelySeconds = c.GetInt32();
                if (first.TryGetProperty("hastily", out var h) && h.ValueKind == JsonValueKind.Number) TtbHastilySeconds = h.GetInt32();
            }
            catch
            {
                // best effort
            }
        }

        private static string? BuildIgdbImage(string? imageId, string size, string ext)
            => string.IsNullOrWhiteSpace(imageId)
                ? null
                : $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.{ext}";
    }
}
