using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    /// <summary>
    /// Representa um utilizador da aplicação.
    ///
    /// Esta entidade guarda:
    /// - dados de autenticação
    /// - dados públicos de perfil
    /// - informação de presença/atividade
    /// - relações sociais (followers/following)
    /// </summary>
    [Table("Users")]
    public class User
    {
        /// <summary>
        /// Chave primária do utilizador.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome de utilizador único ou identificador visível na plataforma.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Nome público/apresentável do utilizador.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Endereço de email do utilizador.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Hash da password do utilizador.
        /// Nunca deve guardar a password em texto simples.
        /// </summary>
        public string? PasswordHash { get; set; }

        /// <summary>
        /// Papel/perfil do utilizador no sistema.
        /// Por defeito: "User".
        /// </summary>
        public string? Role { get; set; } = "User";

        /// <summary>
        /// Data de criação da conta.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Biografia do perfil do utilizador.
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// URL da imagem de avatar.
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// URL da imagem de banner/capa do perfil.
        /// </summary>
        public string? BannerUrl { get; set; }

        /// <summary>
        /// Última vez em que o utilizador foi visto ativo.
        /// </summary>
        public DateTime? LastSeenAt { get; set; }

        /// <summary>
        /// Coleção das relações em que outras pessoas seguem este utilizador.
        /// </summary>
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();

        /// <summary>
        /// Coleção das relações em que este utilizador segue outras pessoas.
        /// </summary>
        public ICollection<Follow> Following { get; set; } = new List<Follow>();
    }
}