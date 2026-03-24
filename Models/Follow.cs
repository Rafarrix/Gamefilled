using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Representa uma relação de follow entre dois utilizadores.
    ///
    /// Exemplo:
    /// - FollowerId  = utilizador que segue
    /// - FollowingId = utilizador que é seguido
    /// </summary>
    [Table("Follows")]
    public class Follow
    {
        /// <summary>
        /// Chave primária da tabela Follows.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador que segue outro utilizador.
        /// </summary>
        [Required]
        public int FollowerId { get; set; }

        /// <summary>
        /// ID do utilizador que está a ser seguido.
        /// </summary>
        [Required]
        public int FollowingId { get; set; }

        /// <summary>
        /// Data de criação da relação de follow.
        /// É inicializada em UTC no momento da criação do objeto.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador que segue.
        /// Ligada à FK FollowerId.
        /// </summary>
        [ForeignKey(nameof(FollowerId))]
        public User? Follower { get; set; }

        /// <summary>
        /// Navegação para o utilizador seguido.
        /// Ligada à FK FollowingId.
        /// </summary>
        [ForeignKey(nameof(FollowingId))]
        public User? Following { get; set; }
    }
}