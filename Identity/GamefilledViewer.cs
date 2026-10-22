using System.Security.Claims;

namespace Gamefilled.Identity;

/// <summary>
/// Identity principal is the ONLY authority for V2 account ownership.
/// Rejects anonymous requests, non-GUID user keys and legacy session-only callers.
/// </summary>
public static class GamefilledViewer
{
    public static Guid RequireUserId(ClaimsPrincipal? viewer)
    {
        if (viewer?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Gamefilled login required.");

        var id = viewer.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid Gamefilled Identity account is required.");

        return userId;
    }
}
