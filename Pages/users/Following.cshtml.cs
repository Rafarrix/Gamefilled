using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    public class FollowingModel : PageModel
    {
        private readonly AppDbContext _db;

        public FollowingModel(AppDbContext db)
        {
            _db = db;
        }

        public User? ProfileUser { get; private set; }
        public List<User> Users { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            ProfileUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (ProfileUser == null)
                return NotFound();

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