using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<UserFavoriteGame> UserFavoriteGames => Set<UserFavoriteGame>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<UserGameEntry> UserGameEntries => Set<UserGameEntry>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureFollows(modelBuilder);
        ConfigureFavoriteGames(modelBuilder);
        ConfigureActivities(modelBuilder);
        ConfigureGameEntries(modelBuilder);
        ConfigureNotifications(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();

        entity.Property(x => x.Username).HasMaxLength(100);
        entity.Property(x => x.DisplayName).HasMaxLength(100);
        entity.Property(x => x.Email).HasMaxLength(150);
        entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(20).IsRequired().HasDefaultValue("User");
        entity.Property(x => x.Bio).HasMaxLength(500);
        entity.Property(x => x.AvatarUrl).HasMaxLength(500);
        entity.Property(x => x.BannerUrl).HasMaxLength(500);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasIndex(x => x.Username)
            .HasDatabaseName("UX_Users_Username")
            .HasFilter("[Username] IS NOT NULL")
            .IsUnique();

        entity.HasIndex(x => x.Email)
            .HasDatabaseName("UX_Users_Email")
            .HasFilter("[Email] IS NOT NULL")
            .IsUnique();

        entity.HasIndex(x => x.LastSeenAt)
            .HasDatabaseName("IX_Users_LastSeenAt")
            .IsDescending()
            .IncludeProperties(x => new { x.Username, x.DisplayName, x.AvatarUrl });
    }

    private static void ConfigureFollows(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Follow>();

        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.Follower)
            .WithMany(x => x.Following)
            .HasForeignKey(x => x.FollowerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Follows_Follower");

        entity.HasOne(x => x.Following)
            .WithMany(x => x.Followers)
            .HasForeignKey(x => x.FollowingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Follows_Following");

        entity.HasIndex(x => new { x.FollowerId, x.FollowingId })
            .HasDatabaseName("UX_Follows_FollowerId_FollowingId")
            .IsUnique();

        entity.ToTable(table => table.HasCheckConstraint(
            "CK_Follows_NoSelfFollow",
            "[FollowerId] <> [FollowingId]"));
    }

    private static void ConfigureFavoriteGames(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserFavoriteGame>();

        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.IsPrimary).HasDefaultValue(false);

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserFavoriteGames_Users");

        entity.HasIndex(x => new { x.UserId, x.GameId })
            .HasDatabaseName("UX_UserFavoriteGames_UserId_GameId")
            .IsUnique();

        entity.HasIndex(x => new { x.UserId, x.SortOrder })
            .HasDatabaseName("UX_UserFavoriteGames_UserId_SortOrder")
            .IsUnique();

        entity.ToTable(table => table.HasCheckConstraint(
            "CK_UserFavoriteGames_SortOrder",
            "[SortOrder] BETWEEN 1 AND 5"));
    }

    private static void ConfigureActivities(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserActivity>();

        entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
        entity.Property(x => x.MetaJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserActivities_Users");

        entity.HasOne(x => x.TargetUser)
            .WithMany()
            .HasForeignKey(x => x.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserActivities_TargetUser");

        entity.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_UserActivities_UserId_CreatedAt")
            .IsDescending(false, true);
    }

    private static void ConfigureGameEntries(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserGameEntry>();

        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
        entity.Property(x => x.ReviewText).HasColumnType("nvarchar(max)");
        entity.Property(x => x.ContainsSpoilers).HasDefaultValue(false);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.User)
            .WithMany(x => x.GameEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserGameEntries_Users");

        entity.HasIndex(x => new { x.UserId, x.GameId })
            .HasDatabaseName("UX_UserGameEntries_UserId_GameId")
            .IsUnique();

        entity.HasIndex(x => new { x.GameId, x.Status })
            .HasDatabaseName("IX_UserGameEntries_GameId_Status");

        entity.HasIndex(x => new { x.UserId, x.UpdatedAt })
            .HasDatabaseName("IX_UserGameEntries_UserId_UpdatedAt")
            .IsDescending(false, true);

        entity.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_UserGameEntries_Status",
                "[Status] IN (N'played', N'playing', N'backlog', N'wishlist')");
            table.HasCheckConstraint(
                "CK_UserGameEntries_Rating",
                "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 10");
            table.HasCheckConstraint(
                "CK_UserGameEntries_ReviewTextLength",
                "[ReviewText] IS NULL OR LEN([ReviewText]) <= 5000");
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserNotification>();

        entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TargetUrl).HasMaxLength(500);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserNotifications_Users_UserId");

        entity.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserNotifications_Users_ActorUserId");

        entity.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_ReadAt_CreatedAt")
            .IsDescending(false, false, true);

        entity.HasIndex(x => new { x.UserId, x.ActorUserId, x.Type, x.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_UserId_ActorUserId_Type_CreatedAt")
            .IsDescending(false, false, false, true);

        entity.ToTable(table => table.HasCheckConstraint(
            "CK_UserNotifications_Type",
            "[Type] IN (N'new_follower', N'mutual_connection')"));
    }
}
