using Gamefilled.Application.Social;
using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.people;

public sealed class IndexModel : PageModel
{
    private const int PageSize = 18;

    private readonly AppDbContext _db;
    private readonly SocialGraphService _socialGraph;

    public IndexModel(AppDbContext db, SocialGraphService socialGraph)
    {
        _db = db;
        _socialGraph = socialGraph;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string Filter { get; set; } = "all";

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public string? CurrentUsername { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(CurrentUsername);
    public IReadOnlyList<PeopleUserCardViewModel> People { get; private set; } = [];
    public int TotalUsers { get; private set; }
    public int TotalPages { get; private set; }
    public int OnlineUsers { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Query = Query?.Trim();
        Filter = NormalizeFilter(Filter);
        PageNumber = Math.Max(1, PageNumber);
        CurrentUsername = HttpContext.Session.GetString("username");

        var utcNow = DateTime.UtcNow;
        var onlineSince = UserPresence.OnlineSince(utcNow);
        var futureLimit = UserPresence.FutureLimit(utcNow);

        var currentUser = string.IsNullOrWhiteSpace(CurrentUsername)
            ? null
            : await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Username == CurrentUsername, cancellationToken);

        var graph = currentUser is null
            ? SocialGraphSnapshot.Empty
            : await _socialGraph.GetSnapshotAsync(currentUser.Id, cancellationToken);

        IQueryable<User> peopleQuery = _db.Users
            .AsNoTracking()
            .Where(user => user.Username != null && user.Username != string.Empty);

        if (!string.IsNullOrWhiteSpace(Query))
        {
            var search = Query;
            peopleQuery = peopleQuery.Where(user =>
                user.Username!.Contains(search) ||
                (user.DisplayName != null && user.DisplayName.Contains(search)));
        }

        peopleQuery = Filter switch
        {
            "online" => peopleQuery.Where(user =>
                user.LastSeenAt != null &&
                user.LastSeenAt >= onlineSince &&
                user.LastSeenAt <= futureLimit),
            "following" when currentUser is not null =>
                peopleQuery.Where(user => graph.FollowingIds.Contains(user.Id)),
            "mutual" when currentUser is not null =>
                peopleQuery.Where(user => graph.MutualIds.Contains(user.Id)),
            "following" or "mutual" => peopleQuery.Where(_ => false),
            _ => peopleQuery
        };

        OnlineUsers = await _db.Users
            .AsNoTracking()
            .CountAsync(user =>
                user.LastSeenAt != null &&
                user.LastSeenAt >= onlineSince &&
                user.LastSeenAt <= futureLimit,
                cancellationToken);

        TotalUsers = await peopleQuery.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalUsers / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);

        var rows = await peopleQuery
            .OrderByDescending(user =>
                user.LastSeenAt != null &&
                user.LastSeenAt >= onlineSince &&
                user.LastSeenAt <= futureLimit)
            .ThenBy(user => user.DisplayName ?? user.Username)
            .ThenBy(user => user.Username)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(user => new PeopleUserRow(
                user.Id,
                user.Username!,
                user.DisplayName,
                user.Bio,
                user.AvatarUrl,
                user.LastSeenAt,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        var ids = rows.Select(row => row.Id).ToArray();
        if (ids.Length == 0)
        {
            People = [];
            return;
        }

        var followerCounts = await _db.Follows
            .AsNoTracking()
            .Where(follow => ids.Contains(follow.FollowingId))
            .GroupBy(follow => follow.FollowingId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        var followingCounts = await _db.Follows
            .AsNoTracking()
            .Where(follow => ids.Contains(follow.FollowerId))
            .GroupBy(follow => follow.FollowerId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        var gameCounts = await _db.UserGameEntries
            .AsNoTracking()
            .Where(entry => ids.Contains(entry.UserId))
            .GroupBy(entry => entry.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        People = rows.Select(row =>
        {
            var isOnline = UserPresence.IsOnline(row.LastSeenAt, utcNow);

            return new PeopleUserCardViewModel
            {
                Id = row.Id,
                Username = row.Username,
                DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Username : row.DisplayName,
                Bio = row.Bio,
                AvatarUrl = row.AvatarUrl,
                IsOnline = isOnline,
                PresenceText = UserPresence.Describe(row.LastSeenAt, utcNow),
                FollowersCount = followerCounts.GetValueOrDefault(row.Id),
                FollowingCount = followingCounts.GetValueOrDefault(row.Id),
                GamesCount = gameCounts.GetValueOrDefault(row.Id),
                IsCurrentUser = currentUser?.Id == row.Id,
                IsFollowing = graph.FollowingIds.Contains(row.Id),
                IsMutual = graph.MutualIds.Contains(row.Id),
                MemberSinceYear = row.CreatedAt.Year
            };
        }).ToList();
    }

    public string PageUrl(int page) => Url.Page(
        "/people/Index",
        new
        {
            q = string.IsNullOrWhiteSpace(Query) ? null : Query,
            filter = Filter == "all" ? null : Filter,
            page
        }) ?? "/people";

    public string FilterUrl(string filter) => Url.Page(
        "/people/Index",
        new
        {
            q = string.IsNullOrWhiteSpace(Query) ? null : Query,
            filter = filter == "all" ? null : filter,
            page = 1
        }) ?? "/people";

    private static string NormalizeFilter(string? filter) => filter?.Trim().ToLowerInvariant() switch
    {
        "online" => "online",
        "following" => "following",
        "mutual" => "mutual",
        _ => "all"
    };

    private sealed record PeopleUserRow(
        int Id,
        string Username,
        string? DisplayName,
        string? Bio,
        string? AvatarUrl,
        DateTime? LastSeenAt,
        DateTime CreatedAt);
}

public sealed class PeopleUserCardViewModel
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsOnline { get; init; }
    public string PresenceText { get; init; } = "Offline";
    public int FollowersCount { get; init; }
    public int FollowingCount { get; init; }
    public int GamesCount { get; init; }
    public bool IsCurrentUser { get; init; }
    public bool IsFollowing { get; init; }
    public bool IsMutual { get; init; }
    public int MemberSinceYear { get; init; }
}
