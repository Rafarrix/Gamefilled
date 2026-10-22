using System.Text.RegularExpressions;
using Xunit;

namespace Gamefilled.Tests;

/// <summary>
/// Migration safety contract: account authority is still session-based in the
/// legacy app. Prevent *more* legacy session dependencies from being added,
/// and reject a half-finished ASP.NET Core Identity authentication cutover.
/// This is temporary and should only become stricter as modules are migrated.
/// </summary>
public sealed class IdentityCutoverGuardTests
{
    private static readonly Regex SessionAccess = new(
        @"\bSession\.(?:GetInt32|GetString|SetInt32|SetString|Clear|Remove)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // This is the measured maximum count of legacy calls per source file at
    // the start of the migration; NOT an approval of the old security model.
    // When moving a module to Identity, reduce/remove its entry here.
    private static readonly IReadOnlyDictionary<string, int> LegacyBaseline =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Application/Social/UserPresenceMiddleware.cs"] = 3,
            ["Infrastructure/ProtectedPageModel.cs"] = 1,
            ["Infrastructure/RequireLoginFilter.cs"] = 1,
            ["Pages/Index.cshtml.cs"] = 2,
            ["Pages/Shared/_Layout.cshtml"] = 3,
            ["Pages/api/NotificationUnread.cshtml.cs"] = 1,
            ["Pages/games/Game.cshtml.cs"] = 2,
            ["Pages/people/Index.cshtml.cs"] = 1,
            ["Pages/settings/Account.cshtml.cs"] = 1,
            ["Pages/settings/Blocked.cshtml.cs"] = 1,
            ["Pages/settings/Defaults.cshtml.cs"] = 1,
            ["Pages/settings/FavoriteGames.cshtml.cs"] = 2,
            ["Pages/settings/Infractions.cshtml.cs"] = 1,
            ["Pages/settings/Integrations.cshtml.cs"] = 1,
            ["Pages/settings/Notifications.cshtml.cs"] = 1,
            ["Pages/settings/Profile.cshtml.cs"] = 5,
            ["Pages/u/Activity.cshtml.cs"] = 1,
            ["Pages/u/Friends.cshtml.cs"] = 1,
            ["Pages/u/Library.cshtml.cs"] = 1,
            ["Pages/u/Profile.cshtml.cs"] = 1,
            ["Pages/users/Follow.cshtml.cs"] = 1,
            ["Pages/users/Followers.cshtml.cs"] = 1,
            ["Pages/users/Following.cshtml.cs"] = 1,
            ["Pages/users/Login.cshtml.cs"] = 7,
            ["Pages/users/Logout.cshtml.cs"] = 1
        };

    [Fact]
    public void Account_session_dependencies_must_never_increase()
    {
        var offenders = ReadCurrentSessionAccessCounts()
            .Where(item => !LegacyBaseline.TryGetValue(item.Key, out var allowed)
                           || item.Value > allowed)
            .Select(item => $"{item.Key}: {item.Value} calls (allowlisted max " +
                            $"{(LegacyBaseline.TryGetValue(item.Key, out var max) ? max : 0)})")
            .OrderBy(x => x)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "New direct session account accesses are forbidden. Migrate existing " +
            "modules to one claims-based Identity principal instead:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Identity_authentication_must_not_run_alongside_legacy_session_authority()
    {
        var root = RepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "Program.cs"));

        // The Identity backend prototype exists separately, but the canonical
        // app must not activate its authentication middleware until *all*
        // existing user/session consumers have been migrated and tested.
        if (!Regex.IsMatch(program, @"\bapp\.UseAuthentication\s*\("))
            return;

        var remaining = ReadCurrentSessionAccessCounts()
            .Where(item => item.Value > 0)
            .Select(item => $"{item.Key}: {item.Value}")
            .OrderBy(x => x)
            .ToArray();

        Assert.True(remaining.Length == 0,
            "Refusing hybrid authorization: Identity middleware is enabled " +
            "while legacy session accesses still exist:\n" +
            string.Join("\n", remaining));
    }

    private static Dictionary<string, int> ReadCurrentSessionAccessCounts()
    {
        var root = RepositoryRoot();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in new[] { "Application", "Infrastructure", "Pages" })
        {
            foreach (var path in Directory.EnumerateFiles(
                Path.Combine(root, directory), "*", SearchOption.AllDirectories))
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var count = SessionAccess.Matches(File.ReadAllText(path)).Count;
                if (count > 0)
                    counts[Path.GetRelativePath(root, path).Replace('\\', '/')] = count;
            }
        }

        return counts;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && current is not null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Gamefilled.csproj")))
                return current.FullName;
        }
        throw new InvalidOperationException("Cannot find Gamefilled.csproj repository root.");
    }
}
