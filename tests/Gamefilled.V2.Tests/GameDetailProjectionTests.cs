using Gamefilled.Infrastructure;
using Gamefilled.V2.Games;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class GameDetailProjectionTests
{
    [Fact]
    public void Projects_game_media_releases_companies_universe_and_similar_without_duplicates()
    {
        var game = Example();
        var view = GameDetailProjection.Project(game);

        Assert.NotNull(view);
        Assert.Equal("Story & Adventure", view.Name);
        Assert.Equal(2024, view.ReleaseYear);
        Assert.Equal(91.5, view.CommunityRating);
        Assert.Equal(180, view.CommunityRatingCount);

        var screenshot = Assert.Single(view.Screenshots);
        Assert.Equal(
            "https://images.igdb.com/igdb/image/upload/t_screenshot_big/co_valid123.jpg",
            screenshot.Url);

        var video = Assert.Single(view.Videos);
        Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", video.WatchUrl);

        Assert.Single(view.ReleaseTimeline);
        Assert.Equal("PC", view.ReleaseTimeline[0].Platform);

        var universe = Assert.Single(view.Universe);
        Assert.Equal("DLC", universe.Title);
        Assert.Equal(32, Assert.Single(universe.Games).Id);
        Assert.Single(view.SimilarGames);
        Assert.Equal(42, view.SimilarGames[0].Id);
        Assert.Equal(2, view.Companies.Count);
    }

    [Fact]
    public void Rejects_untrusted_image_and_video_identifiers_without_building_external_urls()
    {
        var game = Example();
        game.Cover = new IgdbCoverDto { ImageId = "../unsafe" };
        game.Screenshots = [
            new IgdbImageDto { ImageId = "https://evil.invalid/cover" },
            new IgdbImageDto { ImageId = "co_valid123" }
        ];
        game.Artworks = [new IgdbImageDto { ImageId = "co_valid123" }];
        game.Videos = [
            new IgdbVideoDto { VideoId = "https://evil.example/x" },
            new IgdbVideoDto { VideoId = "abcdefghijk" }
        ];

        var result = GameDetailProjection.Project(game)!;
        Assert.Null(result.CoverUrl);
        Assert.Single(result.Screenshots);
        Assert.Single(result.Videos);
        Assert.All(result.Screenshots, item =>
            Assert.StartsWith("https://images.igdb.com/", item.Url));
        Assert.All(result.Videos, item =>
            Assert.StartsWith("https://www.youtube.com/watch?v=", item.WatchUrl));
    }

    [Fact]
    public void Missing_optional_metadata_and_invalid_release_years_do_not_crash()
    {
        var game = new IgdbGameDetailsDto
        {
            Id = 77, Name = " Minimal ",
            FirstReleaseDateUnix = long.MaxValue,
            AggregatedRating = 200
        };
        var view = GameDetailProjection.Project(game);
        Assert.NotNull(view);
        Assert.Null(view.ReleaseYear);
        Assert.Null(view.CommunityRating);
        Assert.Empty(view.Screenshots);
        Assert.Empty(view.Videos);
        Assert.Empty(view.Universe);
        Assert.Empty(view.ReleaseTimeline);
        Assert.Null(GameDetailProjection.Project(new IgdbGameDetailsDto { Id = 0 }));
    }

    [Fact]
    public void Limits_media_related_games_and_release_entries_to_bounded_sizes()
    {
        var game = Example();
        game.Screenshots = Enumerable.Range(1, 50).Select(id => new IgdbImageDto
        {
            ImageId = "image_" + id
        }).ToList();
        game.ReleaseDates = Enumerable.Range(2000, 50).Select(year => new IgdbReleaseDateDto
        {
            Year = year, Human = "Year " + year,
            Platform = new IgdbNamedDto { Id = year, Name = "PC " + year }
        }).ToList();
        game.Dlcs = Enumerable.Range(400, 70).Select(id => new IgdbRelatedGameDto
        {
            Id = id, Name = "DLC " + id
        }).ToList();

        var view = GameDetailProjection.Project(game)!;
        Assert.Equal(8, view.Screenshots.Count);
        Assert.Equal(16, view.ReleaseTimeline.Count);
        Assert.Equal(12, view.Universe.Single(x => x.Title == "DLC").Games.Count);
    }

    private static IgdbGameDetailsDto Example() => new()
    {
        Id = 10,
        Name = " Story & Adventure ",
        Summary = "Example summary",
        FirstReleaseDateUnix = 1713139200,
        Cover = new IgdbCoverDto { ImageId = "co_game" },
        Screenshots = [
            new IgdbImageDto { ImageId = "co_valid123" },
            new IgdbImageDto { ImageId = "co_valid123" }
        ],
        Artworks = [new IgdbImageDto { ImageId = "co_valid123" }],
        Videos = [
            new IgdbVideoDto { VideoId = "abcdefghijk", Name = "Trailer" },
            new IgdbVideoDto { VideoId = "abcdefghijk", Name = "Trailer duplicate" }
        ],
        ReleaseDates = [
            new IgdbReleaseDateDto { Year = 2024, Human = "15 Apr 2024",
                Platform = new IgdbNamedDto { Id = 6, Name = "PC" } }
        ],
        Dlcs = [
            new IgdbRelatedGameDto { Id = 32, Name = "Expansion" },
            new IgdbRelatedGameDto { Id = 32, Name = "Duplicate" }
        ],
        SimilarGames = [
            new IgdbRelatedGameDto { Id = 42, Name = "Related" },
            new IgdbRelatedGameDto { Id = 42, Name = "Duplicate" },
            new IgdbRelatedGameDto { Id = 10, Name = "Self" }
        ],
        InvolvedCompanies = [
            new IgdbInvolvedCompanyDto
            {
                Company = new IgdbCompanyDto { Id = 6, Name = "Studio" },
                Developer = true,
                Publisher = true
            }
        ],
        AggregatedRating = 91.5,
        AggregatedRatingCount = 180
    };
}
