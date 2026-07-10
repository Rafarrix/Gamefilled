using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.people;

public sealed class IndexModel : PageModel
{
    private const int PageSize = 18;
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
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

        var onlineSince = DateTime.UtcNow.Subtract(OnlineWindow);
        var currentUser = string.IsNullOrWhiteSpace(CurrentUsername)
            ? null
            : await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Username == CurrentUsername, cancellationToken);

        var followingIds = new HashSet<int>();
        var followerIds = new HashSet<int>();

        if (currentUser is not null)
        {
            followingIds = (await _db.Follows
                    .AsNoTracking()
                    .Where(follow => follow.FollowerId == currentUser.Id)
                    .Select(follow => follow.FollowingId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            followerIds = (await _db.Follows
                    .AsNoTracking()
                    .Where(follow => follow.FollowingId == currentUser.Id)
                    .Select(follow => follow.FollowerId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var mutualIds = followingIds.Intersect(followerIds).ToHashSet();

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
            "online" => peopleQuery.Where(user => user.LastSeenAt != null && user.LastSeenAt >= onlineSince),
            "following" when currentUser is not null => peopleQuery.Where(user => followingIds.Contains(user.Id)),
            "mutual" when currentUser is not null => peopleQuery.Where(user => mutualIds.Contains(user.Id)),
            "following" or "mutual" => peopleQuery.Where(_ => false),
            _ => peopleQuery
        };

        OnlineUsers = await _db.Users
            .AsNoTracking()
            .CountAsync(user => user.LastSeenAt != null && user.LastSeenAt >= onlineSince, cancellationToken);

        TotalUsers = await peopleQuery.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalUsers / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);

        var rows = await peopleQuery
            .OrderByDescending(user => user.LastSeenAt != null && user.LastSeenAt >= onlineSince)
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
            var isOnline = row.LastSeenAt.HasValue && row.LastSeenAt.Value >= onlineSince;

            return new PeopleUserCardViewModel
            {
                Id = row.Id,
                Username = row.Username,
                DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Username : row.DisplayName,
                Bio = row.Bio,
                AvatarUrl = row.AvatarUrl,
                IsOnline = isOnline,
                PresenceText = BuildPresenceText(row.LastSeenAt, isOnline),
                FollowersCount = followerCounts.GetValueOrDefault(row.Id),
                FollowingCount = followingCounts.GetValueOrDefault(row.Id),
                GamesCount = gameCounts.GetValueOrDefault(row.Id),
                IsCurrentUser = currentUser?.Id == row.Id,
                IsFollowing = followingIds.Contains(row.Id),
                IsMutual = mutualIds.Contains(row.Id),
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

    private static string BuildPresenceText(DateTime? lastSeenAt, bool isOnline)
    {
        if (isOnline)
            return "Online now";

        if (!lastSeenAt.HasValue)
            return "Offline";

        var elapsed = DateTime.UtcNow - lastSeenAt.Value;
        if (elapsed < TimeSpan.FromHours(1))
            return $"Active {Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        if (elapsed < TimeSpan.FromDays(1))
            return $"Active {(int)elapsed.TotalHours}h ago";
        if (elapsed < TimeSpan.FromDays(7))
            return $"Active {(int)elapsed.TotalDays}d ago";

        return "Offline";
    }

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
