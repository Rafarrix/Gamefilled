using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Representa um jogo favorito associado a um utilizador.
    ///
    /// Esta entidade guarda:
    /// - o utilizador dono do favorito
    /// - o ID do jogo na IGDB
    /// - a ordem de apresentação
    /// - se é o favorito principal
    /// </summary>
    [Table("UserFavoriteGames")]
    public class UserFavoriteGame
    {
        /// <summary>
        /// Chave primária do registo.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador dono deste favorito.
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// ID do jogo na IGDB.
        /// </summary>
        [Required]
        public int GameId { get; set; }

        /// <summary>
        /// Ordem em que o jogo aparece na lista de favoritos.
        /// </summary>
        [Required]
        public int SortOrder { get; set; }

        /// <summary>
        /// Indica se este é o jogo favorito principal do utilizador.
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Data de criação do registo.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador dono do favorito.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}