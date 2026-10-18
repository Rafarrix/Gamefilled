using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.u;

public sealed class ReviewsModel : PageModel
{
    public IActionResult OnGet(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return NotFound();

        return Redirect($"/u/{Uri.EscapeDataString(username)}?tab=reviews");
    }
}
