using Gamefilled.Application.Companies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.company;

public sealed class DetailsModel : PageModel
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ICompanyService companyService,
        ILogger<DetailsModel> logger)
    {
        _companyService = companyService;
        _logger = logger;
    }

    public CompanyDetails? Company { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int id,
        string? slug,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            Company = await _companyService.GetDetailsAsync(id, cancellationToken);

            if (Company is null)
                return NotFound();

            if (!string.Equals(slug, Company.Slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPagePermanent(
                    "/company/Details",
                    new { id = Company.Id, slug = Company.Slug });
            }

            return Page();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load IGDB company {CompanyId}.", id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
