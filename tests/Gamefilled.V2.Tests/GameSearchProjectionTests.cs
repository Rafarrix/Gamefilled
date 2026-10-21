using Gamefilled.Infrastructure;
using Gamefilled.V2.Games;
using Xunit;

namespace Gamefilled.V2.Tests;

public sealed class GameSearchProjectionTests
{
    [Fact]
    public void Preserves_IGDB_result_order_without_rewriting_rankings()
    {
        var view = GameSearchProjection.Project([
            new IgdbGameDto { Id = 5, Name = "Second" },
            new IgdbGameDto { Id = 2, Name = "First", Cover = new IgdbCoverDto { ImageId = "co_123" } },
            new IgdbGameDto { Id = 5, Name = "Duplicate" },
            new IgdbGameDto { Id = -1, Name = "Invalid" }
        ]);

        Assert.Equal([5, 2], view.Select(x => x.Id));
        Assert.Equal("Second", view[0].Name);
        Assert.Equal("First", view[1].Name);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co_123.jpg",
            view[1].CoverUrl);
    }

    [Fact]
    public void Rejects_untrusted_cover_identifiers_and_caps_results()
    {
        var many = Enumerable.Range(1, 50).Select(id => new IgdbGameDto
        {
            Id = id,
            Name = "Game " + id,
            Cover = new IgdbCoverDto { ImageId = "../malicious" }
        });
        var games = GameSearchProjection.Project(many);
        Assert.Equal(IgdbGameSearchCatalog.MaxResults, games.Count);
        Assert.All(games, x => Assert.Null(x.CoverUrl));
    }

    [Fact]
    public void Catalog_projection_does_not_parse_or_evaluate_HTML_from_titles()
    {
        var list = GameSearchProjection.Project([
            new IgdbGameDto { Id = 33, Name = "<script>bad()</script>" }
        ]);
        // View model retains source title; Razor owns HTML encoding.
        Assert.Equal("<script>bad()</script>", list[0].Name);
    }
}
