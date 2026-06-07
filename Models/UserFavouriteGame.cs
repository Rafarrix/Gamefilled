using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Modelo que representa um jogo favorito associado a um utilizador.
    ///
    /// O que guarda:
    /// - dono do favorito
    /// - ID do jogo na IGDB
    /// - ordem de apresentação
    /// - indicação de favorito principal
    ///
    /// Importância:
    /// Permite mostrar os favoritos no perfil sem guardar localmente
    /// todos os detalhes do jogo.
    /// </summary>
    [Table("UserFavoriteGames")]
    public class UserFavoriteGame
    {
        /// <summary>
        /// Chave primária do registo.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador dono do favorito.
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
        /// Indica se este é o favorito principal.
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Data de criação do registo.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador dono deste favorito.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}