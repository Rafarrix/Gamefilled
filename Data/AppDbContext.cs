using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<UserFavoriteGame> UserFavoriteGames { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<UserGameEntry> UserGameEntries { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.FollowingId })
                .IsUnique();

            modelBuilder.Entity<UserFavoriteGame>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.SortOrder })
                .IsUnique();

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.GameId })
                .IsUnique();

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.TargetUser)
                .WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserGameEntry>()
                .HasOne(x => x.User)
                .WithMany(x => x.GameEntries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserGameEntry>()
                .HasIndex(x => new { x.UserId, x.GameId })
                .IsUnique();

            modelBuilder.Entity<UserGameEntry>()
                .HasIndex(x => new { x.GameId, x.Status });

            modelBuilder.Entity<UserGameEntry>()
                .HasIndex(x => new { x.UserId, x.UpdatedAt });

            modelBuilder.Entity<UserGameEntry>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_UserGameEntries_Status",
                    "[Status] IN ('played','playing','backlog','wishlist')"));

            modelBuilder.Entity<UserNotification>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserNotification>()
                .HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserNotification>()
                .HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });

            modelBuilder.Entity<UserNotification>()
                .HasIndex(x => new { x.UserId, x.ActorUserId, x.Type, x.CreatedAt });
        }
    }
}
