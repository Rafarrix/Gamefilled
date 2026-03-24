using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    /// <summary>
    /// Página que lista os utilizadores seguidos por um utilizador.
    /// </summary>
    public class FollowingModel : PageModel
    {
        private readonly AppDbContext _db;

        public FollowingModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Utilizador dono da página.
        /// </summary>
        public User? ProfileUser { get; private set; }

        /// <summary>
        /// Lista de utilizadores que ProfileUser segue.
        /// </summary>
        public List<User> Users { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            // Carrega utilizador do perfil.
            ProfileUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (ProfileUser == null)
                return NotFound();

            // Carrega utilizadores seguidos por ordem mais recente.
            Users = await _db.Follows
                .Where(f => f.FollowerId == ProfileUser.Id)
                .Include(f => f.Following)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Following!)
                .ToListAsync();

            return Page();
        }
    }
}