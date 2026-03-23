using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    public class IgdbClient
    {
        private readonly HttpClient _http;
        private readonly IgdbTokenProvider _tokenProvider;
        private readonly IgdbOptions _options;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public IgdbClient(HttpClient http, IgdbTokenProvider tokenProvider, IOptions<IgdbOptions> options)
        {
            _http = http;
            _tokenProvider = tokenProvider;
            _options = options.Value;
        }

        public async Task<List<IgdbGameDto>> SearchGamesAsync(string term, int limit = 10, CancellationToken ct = default)
        {
            term = (term ?? string.Empty).Trim();
            if (term.Length < 2) return new List<IgdbGameDto>();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
search ""{EscapeApicalypseString(term)}"";
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB games search failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        public async Task<IgdbGameDetailsDto?> GetGameDetailsAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return null;

            var body = $@"
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

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB /games failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDetailsDto>>(raw, JsonOpts);
            return data?.FirstOrDefault();
        }

        public async Task<List<IgdbGameDto>> GetTrendingGamesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fiveYearsAgo = DateTimeOffset.UtcNow.AddYears(-5).ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date >= {fiveYearsAgo}
  & first_release_date <= {now}
  & rating_count != null
  & rating_count > 10;
sort rating_count desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB trending failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        public async Task<List<IgdbGameDto>> GetRecentReleasesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date >= {twoYearsAgo}
  & first_release_date <= {now};
sort first_release_date desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB recent releases failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        public async Task<List<IgdbGameDto>> GetTopRatedGamesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date <= {now}
  & aggregated_rating != null
  & aggregated_rating_count != null
  & aggregated_rating_count > 20;
sort aggregated_rating desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB top rated failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        private async Task<HttpRequestMessage> CreateIgdbRequestAsync(string endpoint, string body, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId))
                throw new InvalidOperationException("IGDB ClientId em falta no appsettings.json.");

            if (_http.BaseAddress == null)
                throw new InvalidOperationException("IgdbClient HttpClient BaseAddress não configurado (Program.cs).");

            var token = await _tokenProvider.GetAccessTokenAsync(ct);
            var url = new Uri(_http.BaseAddress, endpoint.TrimStart('/'));

            var req = new HttpRequestMessage(HttpMethod.Post, url);

            req.Headers.Remove("Client-ID");
            req.Headers.Add("Client-ID", _options.ClientId);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            req.Content = new StringContent(body, Encoding.UTF8, "text/plain");
            return req;
        }

        public async Task<List<IgdbGameDto>> GetGamesByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var cleanIds = ids
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (!cleanIds.Any())
                return new List<IgdbGameDto>();

            var idsCsv = string.Join(",", cleanIds);

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where id = ({idsCsv});
limit {cleanIds.Count};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB games by ids failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        private static string EscapeApicalypseString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}