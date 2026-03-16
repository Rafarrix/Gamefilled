using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    public class FollowersModel : PageModel
    {
        private readonly AppDbContext _db;

        public FollowersModel(AppDbContext db)
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
                .Where(f => f.FollowingId == ProfileUser.Id)
                .Include(f => f.Follower)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower!)
                .ToListAsync();

            return Page();
        }
    }
}