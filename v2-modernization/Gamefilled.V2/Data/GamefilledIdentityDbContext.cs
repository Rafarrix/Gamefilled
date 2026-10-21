using Gamefilled.V2.Identity;
using Gamefilled.V2.Library;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.V2.Data;

/// <summary>
/// Isolated post-PAP V2 modernization schema; never point this DbContext at the currently deployed Gamefilleddb.
/// </summary>
public sealed class GamefilledIdentityDbContext
    : IdentityDbContext<GamefilledUser, IdentityRole<Guid>, Guid>
{
    public GamefilledIdentityDbContext(
        DbContextOptions<GamefilledIdentityDbContext> options) : base(options) { }

    public DbSet<GameLibraryEntry> GameLibraryEntries => Set<GameLibraryEntry>();

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

        builder.Entity<GameLibraryEntry>(entry =>
        {
            entry.ToTable("GameLibraryEntries", table =>
            {
                table.HasCheckConstraint("CK_GameLibraryEntries_Status",
                    "[Status] IN (N'played', N'playing', N'backlog', N'wishlist')");
                table.HasCheckConstraint("CK_GameLibraryEntries_Rating",
                    "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_GameLibraryEntries_ReviewLength",
                    "[ReviewText] IS NULL OR LEN([ReviewText]) <= 5000");
                table.HasCheckConstraint("CK_GameLibraryEntries_IgdbGameId", "[IgdbGameId] > 0");
            });
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entry.Property(x => x.ReviewText).HasMaxLength(5000);
            entry.Property(x => x.RowVersion).IsRowVersion();
            entry.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entry.HasIndex(x => new { x.UserId, x.IgdbGameId })
                .IsUnique()
                .HasDatabaseName("UX_GameLibraryEntries_UserId_IgdbGameId");
            entry.HasIndex(x => new { x.UserId, x.UpdatedAtUtc })
                .IsDescending(false, true);
        });
    }
}
