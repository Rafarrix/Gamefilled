using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    [BindProperty(SupportsGet = true)]
    public int? StatusCode { get; set; }

    public string Heading => StatusCode switch
    {
        404 => "Page not found",
        403 => "Access denied",
        _ => "Something went wrong"
    };

    public string Message => StatusCode switch
    {
        404 => "The page may have moved, the link may be outdated or the address may be incomplete.",
        403 => "You do not have permission to open this page.",
        _ => "Gamefilled could not complete this request. Try again or return to discovery."
    };

    public void OnGet(int? statusCode = null)
    {
        StatusCode = statusCode ?? StatusCode ?? 500;
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = StatusCode.Value;
    }
}
