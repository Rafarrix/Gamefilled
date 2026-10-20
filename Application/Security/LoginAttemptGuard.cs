using Microsoft.Extensions.Caching.Memory;

namespace Gamefilled.Application.Security;

/// <summary>
/// Short-lived, per-identifier protection shared by all sessions within one app process.
/// Pair with the per-client-IP rate limiter. This state is not shared across replicas.
/// </summary>
public sealed class LoginAttemptGuard : IDisposable
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StateExpiration = TimeSpan.FromMinutes(15);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object _gate = new();

    private sealed record State(int Failures, DateTimeOffset? LockUntilUtc);

    private static string Key(string identifier) =>
        "login:" + identifier.Trim().ToLowerInvariant();

    public bool IsBlocked(string identifier, out int remainingMinutes)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue<State>(Key(identifier), out var state)
                && state?.LockUntilUtc is { } until && until > DateTimeOffset.UtcNow)
            {
                remainingMinutes = Math.Max(1, (int)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalMinutes));
                return true;
            }
        }

        remainingMinutes = 0;
        return false;
    }

    public void RecordFailure(string identifier)
    {
        lock (_gate)
        {
            var key = Key(identifier);
            var previous = _cache.Get<State>(key);
            var now = DateTimeOffset.UtcNow;
            if (previous?.LockUntilUtc is { } locked && locked > now)
                return;

            // After the lock expires, start a new sequence of attempts.
            var failures = previous?.LockUntilUtc is not null
                ? 1
                : (previous?.Failures ?? 0) + 1;
            var until = failures >= MaxFailures ? now.Add(LockDuration) : (DateTimeOffset?)null;

            _cache.Set(key, new State(failures, until),
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = StateExpiration,
                    Size = 1
                });
        }
    }

    public void RecordSuccess(string identifier)
    {
        lock (_gate)
            _cache.Remove(Key(identifier));
    }

    public void Dispose() => _cache.Dispose();
}
