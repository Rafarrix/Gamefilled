using Gamefilled.V2.Games;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.V2.Pages.Games;

/// <summary>Public, read-only search. Rules still come from the legacy IGDB query.</summary>
[EnableRateLimiting("catalog-search")]
public sealed class SearchModel(
    IGameSearchCatalog search,
    ILogger<SearchModel> logger) : PageModel
{
    public string Query { get; private set; } = "";
    public IReadOnlyList<GameDetailCard> Results { get; private set; } = [];
    public bool Searched { get; private set; }
    public bool Unavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? q, CancellationToken cancellationToken)
    {
        if (q?.Length > IgdbGameSearchCatalog.MaxLength + 20)
            return BadRequest();

        Query = q?.Trim() ?? "";
        Response.Headers.CacheControl = "no-store";
        if (Query.Length < 2)
            return Page();

        if (Query.Length > IgdbGameSearchCatalog.MaxLength)
            return BadRequest();

        Searched = true;
        try
        {
            Results = await search.SearchAsync(Query, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            // Never leak IGDB tokens, request headers or provider response bodies.
            logger.LogWarning("IGDB catalog search temporarily unavailable.");
            Unavailable = true;
        }
        return Page();
    }
}
