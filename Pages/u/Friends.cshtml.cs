using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u
{
    /// <summary>
    /// Página que mostra amizades mútuas de um utilizador.
    /// Aqui "friends" significa:
    /// - o utilizador segue alguém
    /// - e essa pessoa segue-o de volta
    /// </summary>
    public class FriendsModel : PageModel
    {
        private readonly AppDbContext _db;

        public FriendsModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Utilizador dono da página.
        /// </summary>
        public Models.User ProfileUser { get; set; } = default!;

        /// <summary>
        /// Lista de amigos preparados para a UI.
        /// </summary>
        public List<FriendUserViewModel> Friends { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return NotFound();

            ProfileUser = user;

            // Relações em que o utilizador segue alguém.
            var outgoing = _db.Follows
                .Where(f => f.FollowerId == user.Id);

            // Relações em que seguem o utilizador.
            var incoming = _db.Follows
                .Where(f => f.FollowingId == user.Id);

            // Amigos = interseção entre outgoing e incoming.
            Friends = await (
                from o in outgoing
                join i in incoming on o.FollowingId equals i.FollowerId
                join u in _db.Users on o.FollowingId equals u.Id
                orderby u.Username
                select new FriendUserViewModel
                {
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    IsOnlineNow = u.LastSeenAt != null &&
                                  (DateTime.UtcNow - u.LastSeenAt.Value) <= TimeSpan.FromMinutes(5)
                }
            ).ToListAsync(ct);

            return Page();
        }

        /// <summary>
        /// ViewModel interno para a lista de amigos.
        /// </summary>
        public class FriendUserViewModel
        {
            public string? Username { get; set; }
            public string? DisplayName { get; set; }
            public string? AvatarUrl { get; set; }
            public bool IsOnlineNow { get; set; }
        }
    }
}