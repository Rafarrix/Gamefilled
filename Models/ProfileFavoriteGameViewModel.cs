namespace Gamefilled.Models
{
    public class ProfileFavoriteGameViewModel
    {
        public int GameId { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }

        public string? Name { get; set; }
        public string? CoverUrl { get; set; }
    }
}