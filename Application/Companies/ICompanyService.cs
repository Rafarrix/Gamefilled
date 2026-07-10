namespace Gamefilled.Application.Companies;

public interface ICompanyService
{
    Task<CompanyDirectoryResult> SearchAsync(
        string? search,
        int pageNumber = 1,
        int pageSize = 36,
        CancellationToken cancellationToken = default);

    Task<CompanyDetails?> GetDetailsAsync(
        int companyId,
        CancellationToken cancellationToken = default);
}
