using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.V2.Data;

/// <summary>
/// A new schema for V3: never point this DbContext at the V2 Gamefilleddb.
/// </summary>
public sealed class GamefilledIdentityDbContext
    : IdentityDbContext<GamefilledUser, IdentityRole<Guid>, Guid>
{
    public GamefilledIdentityDbContext(
        DbContextOptions<GamefilledIdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GamefilledUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // RequireUniqueEmail is enforced by Identity; the filtered
            // unique index also protects against concurrent duplicate writes.
            entity.HasIndex(x => x.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .HasFilter("[NormalizedEmail] IS NOT NULL")
                .IsUnique();
        });
    }
}
