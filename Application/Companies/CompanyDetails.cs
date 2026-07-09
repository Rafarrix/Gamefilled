using System.Globalization;

namespace Gamefilled.Application.Companies;

public sealed class CompanyDetails
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? CountryCode { get; init; }
    public long? StartDate { get; init; }
    public string? Status { get; init; }
    public string? Size { get; init; }
    public string? LogoImageId { get; init; }
    public CompanyReference? Parent { get; init; }
    public IReadOnlyList<CompanyWebsite> Websites { get; init; } = [];
    public IReadOnlyList<CompanyGameCard> Games { get; init; } = [];

    public string? LogoUrl => CompanyImageUrl.Build(LogoImageId);

    public string? FoundedText => CompanyDateFormatter.Format(StartDate);

    public int DevelopedCount => Games.Count(game => game.IsDeveloper);
    public int PublishedCount => Games.Count(game => game.IsPublisher);
}

public sealed class CompanyDirectoryResult
{
    public IReadOnlyList<CompanyDirectoryCard> Companies { get; init; } = [];
    public string? Search { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CompanyDirectoryCard
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? LogoImageId { get; init; }
    public long? StartDate { get; init; }

    public string? LogoUrl => CompanyImageUrl.Build(LogoImageId);
    public string? FoundedText => CompanyDateFormatter.Format(StartDate, "yyyy");
}

public sealed record CompanyReference(int Id, string Name, string Slug);

public sealed record CompanyWebsite(string Url, string? Type, bool IsTrusted);

public sealed class CompanyGameCard
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? CoverImageId { get; init; }
    public long? FirstReleaseDate { get; init; }
    public double? TotalRating { get; init; }
    public bool IsDeveloper { get; init; }
    public bool IsPublisher { get; init; }
    public bool IsPorting { get; init; }
    public bool IsSupporting { get; init; }

    public string? CoverUrl =>
        string.IsNullOrWhiteSpace(CoverImageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{CoverImageId}.jpg";

    public int? ReleaseYear
    {
        get
        {
            if (FirstReleaseDate is null or <= 0)
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(FirstReleaseDate.Value).Year;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}

internal static class CompanyImageUrl
{
    public static string? Build(string? imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_logo_med/{imageId}.png";
}

internal static class CompanyDateFormatter
{
    public static string? Format(long? unixSeconds, string format = "d MMMM yyyy")
    {
        if (unixSeconds is null or <= 0)
            return null;

        try
        {
            return DateTimeOffset
                .FromUnixTimeSeconds(unixSeconds.Value)
                .ToString(format, CultureInfo.GetCultureInfo("pt-PT"));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
