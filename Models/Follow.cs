using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Modelo que representa uma relação de follow entre dois utilizadores.
    ///
    /// Exemplo:
    /// - FollowerId  = utilizador que segue
    /// - FollowingId = utilizador que é seguido
    ///
    /// Importância:
    /// Esta entidade permite implementar followers, following e amigos.
    /// </summary>
    [Table("Follows")]
    public class Follow
    {
        /// <summary>
        /// Chave primária do registo.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador que segue.
        /// </summary>
        [Required]
        public int FollowerId { get; set; }

        /// <summary>
        /// ID do utilizador que está a ser seguido.
        /// </summary>
        [Required]
        public int FollowingId { get; set; }

        /// <summary>
        /// Data em que a relação foi criada.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador que segue.
        /// </summary>
        [ForeignKey(nameof(FollowerId))]
        public User? Follower { get; set; }

        /// <summary>
        /// Navegação para o utilizador seguido.
        /// </summary>
        [ForeignKey(nameof(FollowingId))]
        public User? Following { get; set; }
    }
}