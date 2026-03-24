namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Classe de configuração da IGDB.
    ///
    /// É preenchida a partir da secção "IGDB" do appsettings.json
    /// através do padrão Options do ASP.NET Core.
    /// </summary>
    public class IgdbOptions
    {
        /// <summary>
        /// Client ID da aplicação registada para acesso à IGDB/Twitch.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Client Secret associado ao Client ID.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;
    }
}