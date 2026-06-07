using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gamefilled.Pages.api
{
    /// <summary>
    /// Endpoint interno responsável por obter os detalhes de um jogo na IGDB.
    ///
    /// O que este ficheiro faz:
    /// - recebe o ID do jogo
    /// - valida a configuração da API
    /// - pede um token ao Twitch
    /// - autentica o pedido à IGDB
    /// - executa a query de detalhe do jogo
    /// - devolve a resposta em JSON ao resto do sistema
    ///
    /// Importância:
    /// Este endpoint evita que a página visual comunique diretamente com a IGDB.
    /// Assim, a lógica da API fica centralizada no back-end.
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class IgdbGameModel : PageModel
    {
        /// <summary>
        /// Configuração do sistema (appsettings).
        /// Aqui é lido o ClientId e ClientSecret.
        /// </summary>
        private readonly IConfiguration _cfg;

        /// <summary>
        /// Logger para registar erros e warnings.
        /// </summary>
        private readonly ILogger<IgdbGameModel> _logger;

        /// <summary>
        /// Factory de HttpClient.
        /// </summary>
        private readonly IHttpClientFactory _http;

        public IgdbGameModel(IConfiguration cfg, ILogger<IgdbGameModel> logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        /// <summary>
        /// Handler GET do endpoint.
        /// Recebe o ID do jogo e devolve os detalhes obtidos da IGDB.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            // Validação básica do id.
            if (id <= 0)
                return new JsonResult(new { error = "id inválido" }) { StatusCode = 400 };

            // Lê as credenciais da configuração.
            var clientId = _cfg["IGDB:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"];

            // Se faltar configuração, devolve erro 500.
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return new JsonResult(new { error = "Falta config IGDB (IGDB:ClientId/IGDB:ClientSecret)" })
                { StatusCode = 500 };

            try
            {
                // =========================================================
                // 1) OBTENÇÃO DO TOKEN TWITCH
                // =========================================================
                // A IGDB exige autenticação via Twitch OAuth.
                var token = await GetTwitchToken(clientId, clientSecret);

                // =========================================================
                // 2) CONFIGURAÇÃO DO HTTPCLIENT
                // =========================================================
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Clear();

                // Header com o Client ID da aplicação.
                client.DefaultRequestHeaders.Add("Client-ID", clientId);

                // Header Authorization com o token recebido do Twitch.
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // =========================================================
                // 3) QUERY APICALYPSE PARA A IGDB
                // =========================================================
                // Esta query pede os campos mais importantes para a página de detalhe.
                var query = $@"
fields
id,name,slug,summary,storyline,first_release_date,
cover.image_id,
artworks.image_id,
screenshots.image_id,
genres.name,
platforms.name,
involved_companies.company.name,
involved_companies.developer,
involved_companies.publisher,
aggregated_rating,aggregated_rating_count,
rating,rating_count,
hypes,follows;
where id = {id};
limit 1;
";

                var content = new StringContent(query, Encoding.UTF8, "text/plain");

                // Faz o pedido à IGDB.
                var res = await client.PostAsync("https://api.igdb.com/v4/games", content);

                // Lê a resposta em JSON.
                var json = await res.Content.ReadAsStringAsync();

                // Se a IGDB devolver erro, regista warning e devolve erro ao sistema.
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IGDB /games falhou: {Status} | Body: {Body}", res.StatusCode, json);
                    return new JsonResult(new { error = "IGDB request falhou", status = (int)res.StatusCode, body = json })
                    { StatusCode = 500 };
                }

                // Devolve o JSON "cru" para a página que fez o pedido.
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no IgdbGame");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        /// <summary>
        /// Obtém um token OAuth do Twitch usando client credentials.
        /// Este token é necessário para autenticar pedidos à IGDB.
        /// </summary>
        private async Task<string> GetTwitchToken(string clientId, string clientSecret)
        {
            var client = _http.CreateClient();

            var url =
                "https://id.twitch.tv/oauth2/token" +
                $"?client_id={clientId}" +
                $"&client_secret={clientSecret}" +
                "&grant_type=client_credentials";

            // Pedido ao endpoint OAuth do Twitch.
            var r = await client.PostAsync(url, null);
            var text = await r.Content.ReadAsStringAsync();

            // Se falhar, lança exceção para ser tratada acima.
            if (!r.IsSuccessStatusCode)
                throw new Exception($"Twitch token failed ({r.StatusCode}): {text}");

            // Faz parse do JSON devolvido pelo Twitch.
            using var jsonDoc = JsonDocument.Parse(text);

            // Extrai o token de acesso.
            return jsonDoc.RootElement.GetProperty("access_token").GetString()!;
        }
    }
}