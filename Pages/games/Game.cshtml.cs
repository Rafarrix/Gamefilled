using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gamefilled.Pages.games
{
    /// <summary>
    /// PageModel da página de detalhe de jogo.
    ///
    /// O que este ficheiro faz:
    /// - recebe o ID do jogo vindo da rota /games/{id}
    /// - pede os dados principais do jogo ao endpoint interno /api/igdbgame
    /// - pede os tempos médios de jogo ao endpoint interno /api/igdbttb
    /// - interpreta os dados recebidos
    /// - prepara as imagens (fundo e cover) para a interface
    ///
    /// Importância no projeto:
    /// Este ficheiro mostra bem a ligação entre:
    /// interface -> lógica -> endpoints internos -> API externa
    /// </summary>
    public class GameModel : PageModel
    {
        /// <summary>
        /// Factory usada para criar HttpClient.
        /// É através dela que a página consegue chamar os endpoints internos.
        /// </summary>
        private readonly IHttpClientFactory _http;

        /// <summary>
        /// Logger da página.
        /// Útil para registar erros e problemas no carregamento do jogo.
        /// </summary>
        private readonly ILogger<GameModel> _logger;

        public GameModel(IHttpClientFactory http, ILogger<GameModel> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>
        /// Objeto com os detalhes principais do jogo.
        /// Vem do endpoint /api/igdbgame.
        /// </summary>
        public IgdbGameDetailsDto? Game { get; private set; }

        /// <summary>
        /// Tempo médio de jogo em modo "normally", em segundos.
        /// </summary>
        public int? TtbNormallySeconds { get; private set; }

        /// <summary>
        /// Tempo para completar o jogo totalmente, em segundos.
        /// </summary>
        public int? TtbCompletelySeconds { get; private set; }

        /// <summary>
        /// Tempo para terminar o jogo em modo rápido, em segundos.
        /// </summary>
        public int? TtbHastilySeconds { get; private set; }

        /// <summary>
        /// URL da imagem de fundo mostrada na página.
        /// </summary>
        public string? BgUrl { get; private set; }

        /// <summary>
        /// URL da capa principal do jogo.
        /// </summary>
        public string? CoverUrl { get; private set; }

        /// <summary>
        /// Handler GET da página.
        ///
        /// Este método é executado quando o utilizador abre a página /games/{id}.
        /// É o ponto de entrada da lógica desta página.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        {
            // Validação básica: um id inválido não pode corresponder a um jogo.
            if (id <= 0)
                return NotFound();

            try
            {
                // Cria um HttpClient para fazer pedidos HTTP.
                var client = _http.CreateClient();

                // Helper para construir URL absoluta, caso o HttpClient não tenha BaseAddress.
                // Isto evita problemas quando o pedido ao endpoint interno precisa do host completo.
                string MakeAbs(string rel)
                {
                    if (client.BaseAddress != null)
                        return rel;

                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    return $"{baseUrl}{rel}";
                }

                // =========================================================
                // 1) PEDIDO DOS DADOS PRINCIPAIS DO JOGO
                // =========================================================
                // Aqui a página chama o endpoint interno do sistema, e não a IGDB diretamente.
                // Isto permite manter a lógica de comunicação externa no back-end.
                var gameRes = await client.GetAsync(MakeAbs($"/api/igdbgame?id={id}"), ct);

                // Lê o conteúdo devolvido pelo endpoint em formato de texto/JSON.
                var gameJson = await gameRes.Content.ReadAsStringAsync(ct);

                // Se o endpoint falhar, regista aviso e devolve erro 500.
                if (!gameRes.IsSuccessStatusCode)
                {
                    _logger.LogWarning("/api/igdbgame falhou: {Status} | Body: {Body}", gameRes.StatusCode, gameJson);
                    return StatusCode(500);
                }

                // Converte o JSON recebido num objeto C#.
                // A resposta vem como lista, mas neste caso esperamos apenas 1 jogo.
                var gameData = JsonSerializer.Deserialize<List<IgdbGameDetailsDto>>(
                    gameJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                );

                // Fica apenas com o primeiro elemento da lista.
                Game = gameData?.FirstOrDefault();

                // Se não vier jogo nenhum, devolve 404.
                if (Game == null)
                    return NotFound();

                // =========================================================
                // 2) PEDIDO DO TIME TO BEAT
                // =========================================================
                // Este pedido é separado porque os tempos médios estão noutro endpoint.
                var ttbRes = await client.GetAsync(MakeAbs($"/api/igdbttb?id={id}"), ct);

                // Best effort:
                // Se falhar, a página continua a funcionar, apenas sem esta informação.
                if (ttbRes.IsSuccessStatusCode)
                {
                    var ttbJson = await ttbRes.Content.ReadAsStringAsync(ct);
                    ParseTtb(ttbJson);
                }

                // =========================================================
                // 3) PREPARAÇÃO DAS IMAGENS
                // =========================================================
                // Prioridade para a imagem de fundo:
                // 1. artwork
                // 2. screenshot
                // 3. cover
                var bgId =
                    Game.Artworks?.FirstOrDefault()?.ImageId ??
                    Game.Screenshots?.FirstOrDefault()?.ImageId ??
                    Game.Cover?.ImageId;

                // Gera a imagem de fundo e a cover principal com tamanhos diferentes.
                BgUrl = BuildIgdbImage(bgId, "t_1080p_2x", "webp");
                CoverUrl = BuildIgdbImage(Game.Cover?.ImageId, "t_cover_big", "jpg");

                // Se tudo correu bem, devolve a página.
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar jogo {Id}", id);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Faz o parse do JSON do endpoint de time-to-beat.
        /// Extrai os valores:
        /// - normally
        /// - completely
        /// - hastily
        /// </summary>
        private void ParseTtb(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                // O endpoint devolve um array.
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return;

                // Como normalmente esperamos apenas um registo, usamos o primeiro.
                var first = doc.RootElement.EnumerateArray().FirstOrDefault();

                if (first.ValueKind != JsonValueKind.Object)
                    return;

                // Se existir a propriedade "normally", guarda-a.
                if (first.TryGetProperty("normally", out var n) && n.ValueKind == JsonValueKind.Number)
                    TtbNormallySeconds = n.GetInt32();

                // Se existir a propriedade "completely", guarda-a.
                if (first.TryGetProperty("completely", out var c) && c.ValueKind == JsonValueKind.Number)
                    TtbCompletelySeconds = c.GetInt32();

                // Se existir a propriedade "hastily", guarda-a.
                if (first.TryGetProperty("hastily", out var h) && h.ValueKind == JsonValueKind.Number)
                    TtbHastilySeconds = h.GetInt32();
            }
            catch
            {
                // Best effort:
                // Se o parse falhar, a página continua a funcionar sem os tempos médios.
            }
        }

        /// <summary>
        /// Constrói a URL completa de uma imagem da IGDB.
        /// Recebe:
        /// - imageId -> ID da imagem na IGDB
        /// - size    -> tamanho/transformação
        /// - ext     -> extensão final
        /// </summary>
        private static string? BuildIgdbImage(string? imageId, string size, string ext)
            => string.IsNullOrWhiteSpace(imageId)
                ? null
                : $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.{ext}";
    }
}