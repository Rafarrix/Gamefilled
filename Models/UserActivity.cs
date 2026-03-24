using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Representa uma atividade registada de um utilizador.
    ///
    /// Exemplos de atividade:
    /// - seguir outro utilizador
    /// - alterar perfil
    /// - adicionar favorito
    /// - ação relacionada com um jogo
    /// </summary>
    [Table("UserActivities")]
    public class UserActivity
    {
        /// <summary>
        /// Chave primária da atividade.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador que executou a atividade.
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Tipo da atividade.
        /// Ex.: "follow", "favorite_added", "profile_updated", etc.
        /// </summary>
        [Required]
        public string Type { get; set; } = "";

        /// <summary>
        /// ID opcional de outro utilizador relacionado com a atividade.
        /// Ex.: numa atividade de follow, este pode ser o utilizador seguido.
        /// </summary>
        public int? TargetUserId { get; set; }

        /// <summary>
        /// ID opcional de jogo relacionado com a atividade.
        /// </summary>
        public int? GameId { get; set; }

        /// <summary>
        /// Campo opcional para guardar metadados adicionais em JSON.
        /// Útil para armazenar informação variável sem alterar a estrutura da tabela.
        /// </summary>
        public string? MetaJson { get; set; }

        /// <summary>
        /// Data de criação da atividade em UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador que executou a atividade.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>
        /// Navegação para o utilizador alvo da atividade, quando aplicável.
        /// </summary>
        [ForeignKey(nameof(TargetUserId))]
        public User? TargetUser { get; set; }
    }
}