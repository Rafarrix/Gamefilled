using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data;

/// <summary>
/// Future canonical single-database V2 data model inside the ACTUAL Gamefilled
/// web project. Not wired into DI until all handlers and routes are migrated.
/// No EF EnsureCreated or automatic migration against running Gamefilleddb.
/// </summary>
public sealed class GamefilledV2DbContext
    : IdentityDbContext<GamefilledAccount, IdentityRole<Guid>, Guid>
{
    public GamefilledV2DbContext(DbContextOptions<GamefilledV2DbContext> options)
        : base(options) { }

    public DbSet<GamefilledGameEntry> GameEntries => Set<GamefilledGameEntry>();
    public DbSet<GamefilledFavoriteGame> FavoriteGames => Set<GamefilledFavoriteGame>();
    public DbSet<GamefilledFollow> Follows => Set<GamefilledFollow>();
    public DbSet<GamefilledActivity> Activities => Set<GamefilledActivity>();
    public DbSet<GamefilledNotification> Notifications => Set<GamefilledNotification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GamefilledAccount>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
            entity.Property(x => x.BannerUrl).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .HasFilter("[NormalizedEmail] IS NOT NULL")
                .IsUnique();
        });

        builder.Entity<GamefilledGameEntry>(entry =>
        {
            entry.ToTable("GamefilledGameEntries", table =>
            {
                table.HasCheckConstraint("CK_GF_GameEntries_GameId", "[IgdbGameId] > 0");
                table.HasCheckConstraint("CK_GF_GameEntries_Status",
                    "[Status] IN (N'played', N'playing', N'backlog', N'wishlist')");
                table.HasCheckConstraint("CK_GF_GameEntries_Rating",
                    "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_GF_GameEntries_Review",
                    "[ReviewText] IS NULL OR LEN([ReviewText]) <= 5000");
            });
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.Property(x => x.Status).IsRequired().HasMaxLength(20);
            entry.Property(x => x.ReviewText).HasMaxLength(5000);
            entry.Property(x => x.RowVersion).IsRowVersion();
            entry.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.HasOne(x => x.User).WithMany(x => x.GameEntries)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entry.HasIndex(x => new { x.UserId, x.IgdbGameId })
                .IsUnique().HasDatabaseName("UX_GF_GameEntries_User_Game");
            entry.HasIndex(x => new { x.UserId, x.UpdatedAtUtc })
                .IsDescending(false, true);
        });

        builder.Entity<GamefilledFavoriteGame>(entry =>
        {
            entry.ToTable("GamefilledFavoriteGames", table =>
            {
                table.HasCheckConstraint("CK_GF_FavoriteGames_GameId", "[IgdbGameId] > 0");
                table.HasCheckConstraint("CK_GF_FavoriteGames_SortOrder",
                    "[SortOrder] BETWEEN 1 AND 5");
            });
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.HasOne(x => x.User).WithMany(x => x.FavoriteGames)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entry.HasIndex(x => new { x.UserId, x.IgdbGameId }).IsUnique();
            entry.HasIndex(x => new { x.UserId, x.SortOrder }).IsUnique();
        });

        builder.Entity<GamefilledFollow>(entry =>
        {
            entry.ToTable("GamefilledFollows", table =>
                table.HasCheckConstraint("CK_GF_Follows_NoSelfFollow",
                    "[FollowerId] <> [FollowingId]"));
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.HasOne(x => x.Follower).WithMany()
                .HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Restrict);
            entry.HasOne(x => x.Following).WithMany()
                .HasForeignKey(x => x.FollowingId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(x => new { x.FollowerId, x.FollowingId }).IsUnique();
            entry.HasIndex(x => new { x.FollowingId, x.CreatedAtUtc });
        });

        builder.Entity<GamefilledActivity>(entry =>
        {
            entry.ToTable("GamefilledActivities");
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.Property(x => x.Type).IsRequired().HasMaxLength(50);
            entry.Property(x => x.MetaJson).HasMaxLength(4000);
            entry.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entry.HasOne(x => x.TargetUser).WithMany()
                .HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
                .IsDescending(false, true);
        });

        builder.Entity<GamefilledNotification>(entry =>
        {
            entry.ToTable("GamefilledNotifications");
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Id).ValueGeneratedNever();
            entry.Property(x => x.Type).IsRequired().HasMaxLength(64);
            entry.Property(x => x.TargetUrl).HasMaxLength(500);
            entry.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entry.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entry.HasOne(x => x.ActorUser).WithMany()
                .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc })
                .IsDescending(false, false, true);
        });
    }
}
