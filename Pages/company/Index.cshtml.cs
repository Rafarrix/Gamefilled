using Gamefilled.Application.Companies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.company;

public sealed class IndexModel : PageModel
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ICompanyService companyService,
        ILogger<IndexModel> logger)
    {
        _companyService = companyService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public CompanyDirectoryResult Result { get; private set; } = new();
    public string? LoadError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Result = await _companyService.SearchAsync(
                Search,
                PageNumber,
                36,
                cancellationToken);

            Search = Result.Search;
            PageNumber = Result.PageNumber;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the company directory.");
            Result = new CompanyDirectoryResult
            {
                Search = Search,
                PageNumber = Math.Max(1, PageNumber),
                PageSize = 36
            };
            LoadError = "Companies could not be loaded. Please try again in a moment.";
        }
    }
}
