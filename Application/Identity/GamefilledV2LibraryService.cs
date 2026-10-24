using System.Security.Claims;
using System.Text.Json;
using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Identity;

/// <summary>
/// Account-scoped, claim-based library operations for the integrated V2.
/// Not yet registered or used by the legacy UI. No int/session user IDs.
/// </summary>
public sealed class GamefilledV2LibraryService(GamefilledV2DbContext database)
{
    public Task<List<GamefilledGameEntry>> MyGamesAsync(
        ClaimsPrincipal viewer, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        return database.GameEntries.AsNoTracking()
            .Where(entry => entry.UserId == owner)
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<GamefilledGameEntry> SaveStatusAsync(
        ClaimsPrincipal viewer, int gameId, string? status, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        var valid = NormalizeStatus(status);
        if (gameId <= 0 || valid is null)
            throw new ArgumentException("Invalid game ID or library status.");

        var row = await database.GameEntries
            .SingleOrDefaultAsync(x => x.UserId == owner && x.IgdbGameId == gameId, ct);
        if (row is null)
        {
            row = new GamefilledGameEntry
            {
                UserId = owner,
                IgdbGameId = gameId,
                Status = valid
            };
            database.GameEntries.Add(row);
        }
        else
        {
            // Preserve manual rating/review when changing status.
            row.Status = valid;
        }

        var now = DateTime.UtcNow;
        row.UpdatedAtUtc = now;
        database.Activities.Add(new GamefilledActivity
        {
            UserId = owner,
            Type = "game_status_changed",
            IgdbGameId = gameId,
            MetaJson = JsonSerializer.Serialize(new { status = valid }),
            CreatedAtUtc = now
        });
        await database.SaveChangesAsync(ct);
        return row;
    }

    public async Task<GamefilledGameEntry> SaveReviewAsync(
        ClaimsPrincipal viewer, int gameId, int? rating,
        string? reviewText, bool spoilers, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        if (gameId <= 0 || rating is < 1 or > 10)
            throw new ArgumentException("Invalid game ID or rating.");

        var review = reviewText?.Trim();
        if (review?.Length > 5000)
            throw new ArgumentException("Review exceeds 5000 characters.");

        var row = await database.GameEntries
            .SingleOrDefaultAsync(x => x.UserId == owner && x.IgdbGameId == gameId, ct);
        if (row is null)
        {
            row = new GamefilledGameEntry
            {
                UserId = owner,
                IgdbGameId = gameId,
                Status = "played"
            };
            database.GameEntries.Add(row);
        }

        row.Rating = rating;
        row.ReviewText = string.IsNullOrWhiteSpace(review) ? null : review;
        row.ContainsSpoilers = spoilers;
        var now = DateTime.UtcNow;
        row.UpdatedAtUtc = now;
        database.Activities.Add(new GamefilledActivity
        {
            UserId = owner,
            Type = row.ReviewText is null ? "rated_game" : "reviewed_game",
            IgdbGameId = gameId,
            MetaJson = JsonSerializer.Serialize(new { rating, containsSpoilers = spoilers }),
            CreatedAtUtc = now
        });
        await database.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> RemoveAsync(
        ClaimsPrincipal viewer, int gameId, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        if (gameId <= 0) return false;
        var row = await database.GameEntries
            .SingleOrDefaultAsync(x => x.UserId == owner && x.IgdbGameId == gameId, ct);
        if (row is null) return false;
        database.GameEntries.Remove(row);
        await database.SaveChangesAsync(ct);
        return true;
    }

    private static string? NormalizeStatus(string? status)
    {
        var value = status?.Trim().ToLowerInvariant();
        return value is "played" or "playing" or "backlog" or "wishlist" ? value : null;
    }
}
