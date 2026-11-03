using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Xunit;

namespace Conduit.IntegrationTests.Features.Articles;

public class BookmarkedTests : SliceFixture
{
    [Theory]
    [InlineData("reader", true)]
    [InlineData("author", false)]
    [InlineData("bystander", false)]
    [InlineData(null, false)]
    public async Task Bookmarked_Is_Viewer_Specific(string? username, bool expected)
    {
        var db = GetDbContext();
        var author = new Person { Username = "author" };
        var reader = new Person { Username = "reader" };
        var bystander = new Person { Username = "bystander" };
        var article = new Article { Slug = "article", Author = author };
        db.AddRange(article, reader, bystander);
        db.Add(new ArticleBookmark { Article = article, Person = reader });
        await db.SaveChangesAsync();
        var accessor = new StubCurrentUserAccessor(
            username,
            username == "reader" ? reader.PersonId
                : username == "author" ? author.PersonId
                : bystander.PersonId
        );

        var details = await new Details.QueryHandler(db, accessor).Handle(
            new Details.Query("article"),
            default
        );
        Assert.Equal(expected, details.Article.Bookmarked);
        Assert.False(details.Article.Favorited);
        Assert.Equal(0, details.Article.FavoritesCount);

        var list = await new List.QueryHandler(db, accessor).Handle(
            new List.Query(null, null, null, null, null),
            default
        );
        Assert.Equal(expected, Assert.Single(list.Articles).Bookmarked);
        Assert.False(Assert.Single(list.Articles).Favorited);
        Assert.Equal(0, Assert.Single(list.Articles).FavoritesCount);
    }
}
