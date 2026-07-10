using Gamefilled.Application.Games;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.u;

public sealed class LibraryModel : PageModel
{
    private readonly UserLibraryService _library;

    public LibraryModel(UserLibraryService library)
    {
        _library = library;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "sort")]
    public string Sort { get; set; } = "recent";

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public UserLibraryResult Result { get; private set; } = default!;
    public string Section { get; private set; } = "games";
    public string Status { get; private set; } = GameLibraryStatus.Played;
    public string Title { get; private set; } = "Played";
    public string Eyebrow { get; private set; } = "Completed library";
    public string Description { get; private set; } = "Games this player has finished or marked as played.";
    public string Icon { get; private set; } = "fas fa-circle-check";
    public bool IsOwnProfile { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string username,
        string section,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || !TryConfigureSection(section))
            return NotFound();

        Query = Query?.Trim();
        Sort = UserLibraryService.NormalizeSort(Sort);
        PageNumber = Math.Max(1, PageNumber);

        var result = await _library.GetAsync(
            username,
            Status,
            Query,
            Sort,
            PageNumber,
            cancellationToken);

        if (result is null)
            return NotFound();

        Result = result;
        PageNumber = result.PageNumber;

        var currentUsername = HttpContext.Session.GetString("username");
        IsOwnProfile = !string.IsNullOrWhiteSpace(currentUsername) &&
            string.Equals(currentUsername, Result.Owner.Username, StringComparison.OrdinalIgnoreCase);

        ApplyPersonalCopy();
        return Page();
    }

    public string LibraryUrl(string section, int page = 1)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(Query))
            query.Add($"q={Uri.EscapeDataString(Query)}");

        if (!string.Equals(Sort, "recent", StringComparison.OrdinalIgnoreCase))
            query.Add($"sort={Uri.EscapeDataString(Sort)}");

        if (page > 1)
            query.Add($"page={page}");

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return $"/u/{Uri.EscapeDataString(Result.Owner.Username)}/{section}{suffix}";
    }

    public string SortUrl(string sort)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(Query))
            query.Add($"q={Uri.EscapeDataString(Query)}");

        var normalized = UserLibraryService.NormalizeSort(sort);
        if (!string.Equals(normalized, "recent", StringComparison.OrdinalIgnoreCase))
            query.Add($"sort={Uri.EscapeDataString(normalized)}");

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return $"/u/{Uri.EscapeDataString(Result.Owner.Username)}/{Section}{suffix}";
    }

    public int Count(string status) => Result.Counts.TryGetValue(status, out var count) ? count : 0;

    private void ApplyPersonalCopy()
    {
        if (!IsOwnProfile)
            return;

        (Eyebrow, Description) = Section switch
        {
            "games" => ("Your completed library", "Games you've finished or marked as played."),
            "playing" => ("Your current rotation", "Games you're playing right now."),
            "backlog" => ("Your backlog", "Games you've saved for later."),
            "wishlist" => ("Your wishlist", "Games you want to pick up."),
            _ => (Eyebrow, Description)
        };
    }

    private bool TryConfigureSection(string? section)
    {
        Section = section?.Trim().ToLowerInvariant() ?? string.Empty;

        switch (Section)
        {
            case "games":
                Status = GameLibraryStatus.Played;
                Title = "Played";
                Eyebrow = "Completed library";
                Description = "Games this player has finished or marked as played.";
                Icon = "fas fa-circle-check";
                return true;
            case "playing":
                Status = GameLibraryStatus.Playing;
                Title = "Playing";
                Eyebrow = "Current rotation";
                Description = "Games currently in progress.";
                Icon = "fas fa-gamepad";
                return true;
            case "backlog":
                Status = GameLibraryStatus.Backlog;
                Title = "Backlog";
                Eyebrow = "Up next";
                Description = "Games saved to play later.";
                Icon = "fas fa-layer-group";
                return true;
            case "wishlist":
                Status = GameLibraryStatus.Wishlist;
                Title = "Wishlist";
                Eyebrow = "Wanted games";
                Description = "Games this player wants to pick up.";
                Icon = "fas fa-heart";
                return true;
            default:
                return false;
        }
    }
}
