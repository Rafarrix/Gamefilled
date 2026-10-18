using Gamefilled.Data;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Social;

public sealed class UserPresenceMiddleware
{
    private const string SessionTouchKey = "presence_last_touch_utc";

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserPresenceMiddleware> _logger;

    public UserPresenceMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<UserPresenceMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await TouchPresenceAsync(context);
        await _next(context);
    }

    private async Task TouchPresenceAsync(HttpContext context)
    {
        var userId = context.Session.GetInt32("userId");
        if (!userId.HasValue || userId.Value <= 0)
            return;

        var now = DateTime.UtcNow;
        var lastTouchValue = context.Session.GetString(SessionTouchKey);

        if (DateTime.TryParse(lastTouchValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastTouch) &&
            now - lastTouch.ToUniversalTime() < UserPresence.TouchInterval)
        {
            return;
        }

        context.Session.SetString(SessionTouchKey, now.ToString("O"));

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Users
                .Where(user => user.Id == userId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.LastSeenAt, now), context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected. Presence is best-effort and must never break the request.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to update presence for user {UserId}.", userId.Value);
        }
    }
}
