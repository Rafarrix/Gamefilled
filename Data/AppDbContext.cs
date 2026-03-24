using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data
{
    /// <summary>
    /// Contexto principal de acesso à base de dados da aplicação.
    ///
    /// Responsabilidades deste DbContext:
    /// - Expor as tabelas principais através de DbSet
    /// - Configurar relações entre entidades
    /// - Definir restrições e índices únicos
    /// - Controlar comportamentos de delete nas foreign keys
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Construtor do contexto.
        /// Recebe as opções configuradas no Program.cs / DI container.
        /// </summary>
        /// <param name="options">Opções do Entity Framework para este contexto.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        /* =====================================================================
           DBSETS
           ---------------------------------------------------------------------
           Cada DbSet representa uma tabela (ou coleção de entidades) na base
           de dados e permite fazer queries/inserts/updates/deletes com EF Core.
           ===================================================================== */

        /// <summary>
        /// Tabela de utilizadores da aplicação.
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Tabela que representa relações de follow entre utilizadores.
        /// Ex.: User A segue User B.
        /// </summary>
        public DbSet<Follow> Follows { get; set; }

        /// <summary>
        /// Tabela dos jogos favoritos definidos por cada utilizador.
        /// </summary>
        public DbSet<UserFavoriteGame> UserFavoriteGames { get; set; }

        /// <summary>
        /// Tabela de atividades dos utilizadores.
        /// Ex.: seguir alguém, atualizar perfil, etc.
        /// </summary>
        public DbSet<UserActivity> UserActivities { get; set; }

        /* =====================================================================
           MODEL CONFIGURATION
           ---------------------------------------------------------------------
           Aqui configuramos detalhes que não ficam apenas pelas convenções
           automáticas do EF Core:
           - relações explícitas
           - índices únicos
           - regras de delete
           ===================================================================== */
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Mantém qualquer configuração base do EF/Core.
            base.OnModelCreating(modelBuilder);

            /* -----------------------------------------------------------------
               FOLLOW
               -----------------------------------------------------------------
               A entidade Follow representa uma relação entre dois utilizadores:
               - Follower   -> quem segue
               - Following  -> quem é seguido
               ----------------------------------------------------------------- */

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);
            // Restrict evita loops/cascatas perigosas entre utilizadores
            // numa relação autorreferenciada.

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);
            // Também usamos Restrict no lado do utilizador seguido
            // para impedir deletes automáticos em cadeia.

            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.FollowingId })
                .IsUnique();
            // Garante que o mesmo utilizador não pode seguir a mesma pessoa
            // mais do que uma vez.

            /* -----------------------------------------------------------------
               USER FAVORITE GAME
               -----------------------------------------------------------------
               Esta entidade associa um utilizador a jogos favoritos e permite
               guardar ordenação/posição dos favoritos.
               ----------------------------------------------------------------- */

            modelBuilder.Entity<UserFavoriteGame>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Se o utilizador for apagado, os seus favoritos também são apagados.

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.SortOrder })
                .IsUnique();
            // Garante que um utilizador não tem duas entradas com a mesma posição
            // na lista de favoritos.

            modelBuilder.Entity<UserFavoriteGame>()
                .HasIndex(x => new { x.UserId, x.GameId })
                .IsUnique();
            // Garante que o mesmo jogo não pode ser adicionado duas vezes
            // aos favoritos do mesmo utilizador.

            /* -----------------------------------------------------------------
               USER ACTIVITY
               -----------------------------------------------------------------
               Regista eventos/ações relacionados com um utilizador.
               Pode também apontar para outro utilizador envolvido na ação.
               ----------------------------------------------------------------- */

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            // O autor da atividade não pode provocar delete em cascata
            // automático neste registo.

            modelBuilder.Entity<UserActivity>()
                .HasOne(x => x.TargetUser)
                .WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
            // O utilizador alvo da atividade também usa Restrict,
            // evitando múltiplos caminhos de delete ou conflitos relacionais.
        }
    }
}