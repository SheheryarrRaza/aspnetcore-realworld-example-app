using System;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Xunit;

namespace Conduit.IntegrationTests.Features.Articles;

public class BookmarkedTests : SliceFixture
{
    [Theory]
    [InlineData("bookmarker", true)]
    [InlineData("author", false)]
    [InlineData("bystander", false)]
    [InlineData(null, false)]
    public async Task Bookmarked_Is_Viewer_Specific(string? username, bool expected)
    {
        var db = GetDbContext();
        var author = new Person { Username = "author" };
        var bookmarker = new Person { Username = "bookmarker" };
        var article = new Article { Slug = "article", Author = author };
        db.AddRange(article, bookmarker, new Person { Username = "bystander" });
        db.Add(
            new ArticleBookmark
            {
                Article = article,
                Person = bookmarker,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();
        var accessor = new StubCurrentUserAccessor(
            username,
            username == "bookmarker" ? bookmarker.PersonId
                : username == "author" ? author.PersonId
                : 3
        );
        var details = await new Details.QueryHandler(db, accessor).Handle(
            new Details.Query("article"),
            default
        );
        Assert.Equal(expected, details.Article.Bookmarked);
        var list = await new List.QueryHandler(db, accessor).Handle(
            new List.Query(null, null, null, null, null),
            default
        );
        Assert.Equal(expected, Assert.Single(list.Articles).Bookmarked);
    }
}
