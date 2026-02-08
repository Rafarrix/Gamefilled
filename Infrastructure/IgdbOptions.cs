namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Configuração do IGDB (vem do appsettings.json -> "IGDB")
    /// </summary>
    public class IgdbOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}