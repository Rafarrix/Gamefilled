using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.games
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // ✅ /games redireciona sempre para o filtro principal
            return RedirectToPage("/games/lib/Popular");
        }
    }
}
