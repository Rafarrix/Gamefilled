using System.Security.Claims;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.V2.Library;

/// <summary>
/// Mutations always derive ownership from the authenticated Identity claims.
/// No caller can pass a target userId, preventing cross-account modification.
/// </summary>
public sealed class GameLibraryService(
    GamefilledIdentityDbContext database,
    UserManager<GamefilledUser> users)
{
    private Guid RequireViewer(ClaimsPrincipal viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (viewer.Identity?.IsAuthenticated != true
            || !Guid.TryParse(users.GetUserId(viewer), out var id)
            || id == Guid.Empty)
            throw new UnauthorizedAccessException("Authentication required.");

        return id;
    }

    public Task<List<GameLibraryEntry>> MyGamesAsync(ClaimsPrincipal viewer, CancellationToken ct = default)
    {
        var userId = RequireViewer(viewer);
        return database.GameLibraryEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .ThenBy(entry => entry.IgdbGameId)
            .ToListAsync(ct);
    }

    public async Task<GameLibraryEntry> SaveStatusAsync(
        ClaimsPrincipal viewer, int igdbGameId, string? status, CancellationToken ct = default)
    {
        var userId = RequireViewer(viewer);
        if (igdbGameId <= 0)
            throw new ArgumentOutOfRangeException(nameof(igdbGameId));
        var normalized = GameLibraryStatus.Normalize(status)
            ?? throw new ArgumentException("Invalid game status.", nameof(status));

        var entry = await database.GameLibraryEntries
            .SingleOrDefaultAsync(e => e.UserId == userId && e.IgdbGameId == igdbGameId, ct);
        if (entry is null)
        {
            entry = new GameLibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IgdbGameId = igdbGameId,
                Status = normalized
            };
            database.GameLibraryEntries.Add(entry);
        }
        else
        {
            entry.Status = normalized;
            entry.UpdatedAtUtc = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<GameLibraryEntry> SaveReviewAsync(
        ClaimsPrincipal viewer,
        int igdbGameId,
        int? rating,
        string? reviewText,
        bool containsSpoilers,
        CancellationToken ct = default)
    {
        var userId = RequireViewer(viewer);
        if (igdbGameId <= 0)
            throw new ArgumentOutOfRangeException(nameof(igdbGameId));
        if (rating is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(rating));
        if (reviewText?.Length > 5000)
            throw new ArgumentException("Review exceeds 5000 characters.", nameof(reviewText));

        var entry = await database.GameLibraryEntries
            .SingleOrDefaultAsync(e => e.UserId == userId && e.IgdbGameId == igdbGameId, ct);
        if (entry is null)
        {
            entry = new GameLibraryEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IgdbGameId = igdbGameId,
                Status = GameLibraryStatus.Backlog
            };
            database.GameLibraryEntries.Add(entry);
        }

        entry.Rating = rating;
        entry.ReviewText = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText;
        entry.ContainsSpoilers = entry.ReviewText is not null && containsSpoilers;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> RemoveAsync(
        ClaimsPrincipal viewer, int igdbGameId, CancellationToken ct = default)
    {
        var userId = RequireViewer(viewer);
        if (igdbGameId <= 0)
            throw new ArgumentOutOfRangeException(nameof(igdbGameId));

        var entry = await database.GameLibraryEntries.SingleOrDefaultAsync(
            x => x.UserId == userId && x.IgdbGameId == igdbGameId, ct);
        if (entry is null)
            return false;

        database.GameLibraryEntries.Remove(entry);
        await database.SaveChangesAsync(ct);
        return true;
    }
}
