using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Modelo que representa uma atividade registada no sistema.
    ///
    /// Exemplos:
    /// - seguir outro utilizador
    /// - alterar perfil
    /// - adicionar favorito
    /// - ações relacionadas com jogos
    ///
    /// Importância:
    /// Permite construir o histórico de atividade mostrado no perfil.
    /// </summary>
    [Table("UserActivities")]
    public class UserActivity
    {
        /// <summary>
        /// Chave primária da atividade.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID do utilizador que realizou a ação.
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Tipo da atividade.
        /// Ex.: "followed_user", "updated_favorites", etc.
        /// </summary>
        [Required]
        public string Type { get; set; } = "";

        /// <summary>
        /// ID opcional de outro utilizador relacionado com a atividade.
        /// </summary>
        public int? TargetUserId { get; set; }

        /// <summary>
        /// ID opcional de jogo relacionado com a atividade.
        /// </summary>
        public int? GameId { get; set; }

        /// <summary>
        /// Campo opcional para metadados adicionais em JSON.
        /// </summary>
        public string? MetaJson { get; set; }

        /// <summary>
        /// Data de criação da atividade.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navegação para o utilizador que executou a atividade.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        /// <summary>
        /// Navegação para o utilizador alvo da atividade.
        /// </summary>
        [ForeignKey(nameof(TargetUserId))]
        public User? TargetUser { get; set; }
    }
}