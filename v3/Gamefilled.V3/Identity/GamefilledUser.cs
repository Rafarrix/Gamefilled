using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Gamefilled.V3.Identity;

/// <summary>
/// Canonical account entity for Gamefilled V3. All future social/library entities
/// must reference this identity, not the legacy V2 session user ID.
/// </summary>
public sealed class GamefilledUser : IdentityUser<Guid>
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
