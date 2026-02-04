using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.Settings
{
    public class DefaultsModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("userId") == null)
                return RedirectToPage("/Users/Login");

            return Page();
        }
    }
}
