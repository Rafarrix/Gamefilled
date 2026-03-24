namespace Gamefilled.Models
{
    /// <summary>
    /// ViewModel usado para mostrar jogos favoritos no perfil.
    ///
    /// Nota:
    /// Este tipo não representa diretamente uma tabela da base de dados.
    /// Serve para transportar dados já preparados para a UI.
    /// </summary>
    public class ProfileFavoriteGameViewModel
    {
        /// <summary>
        /// ID do jogo na IGDB.
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// Ordem em que o jogo aparece na lista de favoritos.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Indica se este jogo é o favorito principal.
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Nome do jogo, já pronto para mostrar na interface.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// URL da capa do jogo, já resolvida para consumo na UI.
        /// </summary>
        public string? CoverUrl { get; set; }
    }
}