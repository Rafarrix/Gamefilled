using System.Globalization;

namespace Gamefilled.Application.Companies;

public static class CompanyCountryDisplay
{
    public static string EnglishName(this CompanyCountry country)
    {
        try
        {
            return new RegionInfo(country.Alpha2).EnglishName;
        }
        catch (ArgumentException)
        {
            return country.Name;
        }
    }
}
