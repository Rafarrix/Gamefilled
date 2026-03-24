using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    /// <summary>
    /// Página que lista os followers de um utilizador.
    /// </summary>
    public class FollowersModel : PageModel
    {
        private readonly AppDbContext _db;

        public FollowersModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Utilizador dono da página.
        /// </summary>
        public User? ProfileUser { get; private set; }

        /// <summary>
        /// Lista de utilizadores que seguem ProfileUser.
        /// </summary>
        public List<User> Users { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            // Carrega o utilizador dono do perfil.
            ProfileUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (ProfileUser == null)
                return NotFound();

            // Carrega os followers por ordem mais recente.
            Users = await _db.Follows
                .Where(f => f.FollowingId == ProfileUser.Id)
                .Include(f => f.Follower)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower!)
                .ToListAsync();

            return Page();
        }
    }
}