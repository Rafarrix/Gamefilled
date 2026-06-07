using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data
{
    /// <summary>
    /// Contexto principal de acesso à base de dados da aplicação.
    ///
    /// O que este ficheiro faz:
    /// - expõe as tabelas principais através de DbSet
    /// - configura relações entre entidades
    /// - define índices únicos
    /// - controla regras de delete em foreign keys
    ///
    /// Importância:
    /// É o centro da comunicação entre o código C# e a base de dados.
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Construtor do contexto.
        /// Recebe as opções configuradas no Program.cs.
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        /* =====================================================================
           DBSETS
           ---------------------------------------------------------------------
           Cada DbSet representa uma tabela da base de dados.
           ===================================================================== */

        /// <summary>
        /// Tabela de utilizadores.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Tabela que representa relações de follow entre utilizadores.
        /// </summary>
        public DbSet<Follow> Follows { get; set; }

        /// <summary>
        /// Tabela dos jogos favoritos dos utilizadores.
        /// </summary>
        public DbSet<UserFavoriteGame> UserFavoriteGames { get; set; }

        /// <summary>
        /// Tabela das atividades dos utilizadores.
        /// </summary>
        public DbSet<UserActivity> UserActivities { get; set; }

        /* =====================================================================
           CONFIGURAÇÃO DO MODELO
           ---------------------------------------------------------------------
           Aqui definimos relações explícitas, índices e regras que vão além
           das convenções automáticas do Entity Framework Core.
           ===================================================================== */
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Mantém qualquer configuração base do EF Core.
            base.OnModelCreating(modelBuilder);

            /* -----------------------------------------------------------------
               FOLLOW
               -----------------------------------------------------------------
               Uma relação Follow liga dois utilizadores:
               - Follower  -> quem segue
               - Following -> quem é seguido
               ----------------------------------------------------------------- */

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);
            // Restrict evita deletes em cascata problemáticos numa relação autorreferenciada.

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);
            // Também se usa Restrict no lado do utilizador seguido.

            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.FollowingId })
                .IsUnique();
            // Garante que não há follows duplicados entre os mesmos dois utilizadores.

            /* -----------------------------------------------------------------
               USER FAVORITE GAME
               -----------------------------------------------------------------
               Guarda os jogos favoritos de cada utilizador e a ordem em que
               aparecem no perfil.
               ----------------------------------------------------------------- */

            modelBuilder.Entity<UserFavoriteGame>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Se o utilizador for apagado, os seus favoritos também são removidos.

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.SortOrder })
                .IsUnique();
            // Garante que o utilizador não repete a mesma posição na lista.

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.GameId })
                .IsUnique();
            // Garante que o mesmo jogo não é adicionado duas vezes aos favoritos.

            /* -----------------------------------------------------------------
               USER ACTIVITY
               -----------------------------------------------------------------
               Guarda ações relacionadas com um utilizador.
               Pode apontar também para outro utilizador alvo.
               ----------------------------------------------------------------- */

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            // O autor da atividade usa Restrict para evitar cascatas automáticas.

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.TargetUser)
                .WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
            // O utilizador alvo também usa Restrict para evitar conflitos relacionais.
        }
    }
}