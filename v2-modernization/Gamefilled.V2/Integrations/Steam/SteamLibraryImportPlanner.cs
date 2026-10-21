namespace Gamefilled.V2.Integrations.Steam;

/// <summary>
/// A Steam-owned title is evidence of ownership only, not that the user has
/// played or completed the game. No Steam login credential is ever stored.
/// </summary>
public sealed record SteamOwnedGame(uint AppId, uint PlaytimeMinutes, string? Name);

public sealed record VerifiedGameMatch(uint SteamAppId, int IgdbGameId);

public sealed record SteamOwnershipLink(uint SteamAppId, int IgdbGameId, uint PlaytimeMinutes);

public sealed record SteamImportPlan(
    IReadOnlyList<SteamOwnershipLink> ToLink,
    IReadOnlyList<SteamOwnershipLink> AlreadyLinked,
    IReadOnlyList<SteamOwnedGame> Unmatched,
    int DuplicateEntriesSkipped);

/// <summary>
/// Pure, side-effect-free preview of what a user-approved Steam import would do.
/// `matches` MUST have been verified by the server using IGDB external_games
/// Steam IDs (not title string similarity or user-supplied IDs).
/// A future authenticated service will apply this plan only to the signed-in
/// Identity user's own records, with an atomic database transaction.
/// </summary>
public static class SteamLibraryImportPlanner
{
    public static SteamImportPlan Preview(
        IEnumerable<SteamOwnedGame> ownedGames,
        IEnumerable<VerifiedGameMatch> matches,
        IReadOnlySet<uint> existingSteamAppIds)
    {
        ArgumentNullException.ThrowIfNull(ownedGames);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(existingSteamAppIds);

        // Conflicting mappings are deliberately discarded for manual review.
        var verified = matches
            .Where(x => x.SteamAppId > 0 && x.IgdbGameId > 0)
            .GroupBy(x => x.SteamAppId)
            .Where(g => g.Select(x => x.IgdbGameId).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().IgdbGameId);

        var toLink = new List<SteamOwnershipLink>();
        var present = new List<SteamOwnershipLink>();
        var unmatched = new List<SteamOwnedGame>();
        var seen = new HashSet<uint>();
        var seenGameIds = new HashSet<int>();
        var duplicate = 0;

        foreach (var item in ownedGames.OrderBy(x => x.AppId).ThenByDescending(x => x.PlaytimeMinutes))
        {
            if (item.AppId == 0 || !seen.Add(item.AppId))
            {
                duplicate++;
                continue;
            }

            if (!verified.TryGetValue(item.AppId, out var igdbId))
            {
                unmatched.Add(item);
                continue;
            }

            if (!seenGameIds.Add(igdbId))
            {
                duplicate++;
                continue;
            }

            var link = new SteamOwnershipLink(item.AppId, igdbId, item.PlaytimeMinutes);
            if (existingSteamAppIds.Contains(item.AppId))
                present.Add(link);
            else
                toLink.Add(link);
        }

        return new SteamImportPlan(toLink, present, unmatched, duplicate);
    }
}
