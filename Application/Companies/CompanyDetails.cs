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

    public string? LogoUrl =>
        string.IsNullOrWhiteSpace(LogoImageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_logo_med/{LogoImageId}.png";

    public string? FoundedText
    {
        get
        {
            if (StartDate is null or <= 0)
                return null;

            try
            {
                return DateTimeOffset
                    .FromUnixTimeSeconds(StartDate.Value)
                    .ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("pt-PT"));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }

    public int DevelopedCount => Games.Count(game => game.IsDeveloper);
    public int PublishedCount => Games.Count(game => game.IsPublisher);
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
