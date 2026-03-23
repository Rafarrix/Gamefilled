using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.Settings
{
    public class ProfileModel : PageModel
    {
        private readonly AppDbContext _db;

        public ProfileModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string? DisplayName { get; set; }

        [BindProperty]
        public string? Bio { get; set; }

        [BindProperty]
        public string? AvatarUrl { get; set; }

        [BindProperty]
        public string? BannerUrl { get; set; }

        public string? CurrentUsername { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/users/Login");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return RedirectToPage("/");

            CurrentUsername = user.Username;
            DisplayName = user.DisplayName;
            Bio = user.Bio;
            AvatarUrl = user.AvatarUrl;
            BannerUrl = user.BannerUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/users/Login");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
                return RedirectToPage("/");

            var cleanedDisplayName = string.IsNullOrWhiteSpace(DisplayName) ? user.Username : DisplayName.Trim();
            var cleanedBio = string.IsNullOrWhiteSpace(Bio) ? null : Bio.Trim();

            if (!string.IsNullOrWhiteSpace(cleanedBio))
            {
                var wordCount = cleanedBio
                    .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                if (wordCount > 200)
                {
                    ModelState.AddModelError(nameof(Bio), "Bio cannot have more than 200 words.");
                    CurrentUsername = user.Username;
                    DisplayName = cleanedDisplayName;
                    AvatarUrl = string.IsNullOrWhiteSpace(AvatarUrl) ? null : AvatarUrl.Trim();
                    BannerUrl = string.IsNullOrWhiteSpace(BannerUrl) ? null : BannerUrl.Trim();
                    return Page();
                }
            }

            user.DisplayName = cleanedDisplayName;
            user.Bio = cleanedBio;
            user.AvatarUrl = string.IsNullOrWhiteSpace(AvatarUrl) ? null : AvatarUrl.Trim();
            user.BannerUrl = string.IsNullOrWhiteSpace(BannerUrl) ? null : BannerUrl.Trim();

            await _db.SaveChangesAsync();

            return Redirect($"/u/{username}");
        }
    }
}