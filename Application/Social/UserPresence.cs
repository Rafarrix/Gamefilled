namespace Gamefilled.Application.Social;

public static class UserPresence
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan FutureClockTolerance = TimeSpan.FromMinutes(1);

    public static DateTime OnlineSince(DateTime utcNow) => utcNow.Subtract(OnlineWindow);

    public static DateTime FutureLimit(DateTime utcNow) => utcNow.Add(FutureClockTolerance);

    public static bool IsOnline(DateTime? lastSeenAt, DateTime utcNow)
    {
        if (!lastSeenAt.HasValue)
            return false;

        var value = DateTime.SpecifyKind(lastSeenAt.Value, DateTimeKind.Utc);
        return value >= OnlineSince(utcNow) && value <= FutureLimit(utcNow);
    }

    public static string Describe(DateTime? lastSeenAt, DateTime utcNow)
    {
        if (IsOnline(lastSeenAt, utcNow))
            return "Online now";

        if (!lastSeenAt.HasValue)
            return "Offline";

        var value = DateTime.SpecifyKind(lastSeenAt.Value, DateTimeKind.Utc);
        if (value > FutureLimit(utcNow))
            return "Offline";

        var elapsed = utcNow - value;
        if (elapsed < TimeSpan.Zero)
            return "Offline";
        if (elapsed < TimeSpan.FromHours(1))
            return $"Active {Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        if (elapsed < TimeSpan.FromDays(1))
            return $"Active {Math.Max(1, (int)elapsed.TotalHours)}h ago";
        if (elapsed < TimeSpan.FromDays(7))
            return $"Active {Math.Max(1, (int)elapsed.TotalDays)}d ago";

        return "Offline";
    }
}
