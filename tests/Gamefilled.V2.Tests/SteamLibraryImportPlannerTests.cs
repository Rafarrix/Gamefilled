using Gamefilled.V2.Integrations.Steam;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class SteamLibraryImportPlannerTests
{
    [Fact]
    public void Imports_only_games_with_exact_verified_Steam_to_IGDB_mapping()
    {
        var plan = SteamLibraryImportPlanner.Preview(
            new[]
            {
                new SteamOwnedGame(440, 250, "Team Fortress 2"),
                new SteamOwnedGame(570, 35, "Dota 2"),
                new SteamOwnedGame(123456, 0, "Unmatched title")
            },
            new[]
            {
                new VerifiedGameMatch(440, 123),
                new VerifiedGameMatch(570, 456)
            },
            new HashSet<uint>());

        Assert.Equal(2, plan.ToLink.Count);
        Assert.Single(plan.Unmatched);
        Assert.Equal((uint)123456, plan.Unmatched[0].AppId);
        Assert.Equal((uint)250, plan.ToLink.Single(x => x.SteamAppId == 440).PlaytimeMinutes);
    }

    [Fact]
    public void Repeated_import_will_not_duplicate_already_linked_games()
    {
        var games = new[] { new SteamOwnedGame(440, 360, "Title") };
        var map = new[] { new VerifiedGameMatch(440, 101) };

        var plan = SteamLibraryImportPlanner.Preview(games, map, new HashSet<uint> { 440 });

        Assert.Empty(plan.ToLink);
        Assert.Single(plan.AlreadyLinked);
        Assert.Equal((uint)360, plan.AlreadyLinked[0].PlaytimeMinutes);
    }

    [Fact]
    public void Conflicting_verified_mappings_are_not_automatically_imported()
    {
        var plan = SteamLibraryImportPlanner.Preview(
            new[] { new SteamOwnedGame(10, 0, "Ambiguous") },
            new[] { new VerifiedGameMatch(10, 3), new VerifiedGameMatch(10, 7) },
            new HashSet<uint>());

        Assert.Empty(plan.ToLink);
        Assert.Single(plan.Unmatched);
    }

    [Fact]
    public void Duplicate_steam_results_or_multiple_apps_for_same_game_are_not_double_linked()
    {
        var plan = SteamLibraryImportPlanner.Preview(
            new[]
            {
                new SteamOwnedGame(10, 1, "A"),
                new SteamOwnedGame(10, 90, "A"),
                new SteamOwnedGame(20, 30, "Same IGDB title")
            },
            new[] { new VerifiedGameMatch(10, 100), new VerifiedGameMatch(20, 100) },
            new HashSet<uint>());

        Assert.Single(plan.ToLink);
        Assert.Equal((uint)90, plan.ToLink[0].PlaytimeMinutes);
        Assert.Equal(2, plan.DuplicateEntriesSkipped);
    }

    [Fact]
    public void User_supplied_titles_cannot_create_mappings()
    {
        var plan = SteamLibraryImportPlanner.Preview(
            new[] { new SteamOwnedGame(7788, 40, "Exact looking title") },
            Array.Empty<VerifiedGameMatch>(),
            new HashSet<uint>());
        Assert.Empty(plan.ToLink);
        Assert.Single(plan.Unmatched);
    }
}
