namespace Gamefilled.Application.Companies;

public interface ICompanyService
{
    Task<CompanyDetails?> GetDetailsAsync(
        int companyId,
        CancellationToken cancellationToken = default);
}
