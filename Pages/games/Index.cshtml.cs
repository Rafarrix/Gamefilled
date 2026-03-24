using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.games
{
    /// <summary>
    /// Página índice de /games.
    ///
    /// Em vez de renderizar conteúdo próprio,
    /// redireciona sempre para o filtro principal da biblioteca.
    /// </summary>
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // /games -> /games/lib/Popular
            return RedirectToPage("/games/lib/Popular");
        }
    }
}