using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.IntegrationTests.Features.Articles;
using Conduit.IntegrationTests.Features.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conduit.IntegrationTests.Features.Bookmarks;

public class AddTests : SliceFixture
{
    [Fact]
    public async Task Expect_Bookmark_Add_Sets_Bookmarked_And_Is_Idempotent()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Bookmark add article",
                Description = "Description",
                Body = "Body",
            }
        );
        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug!;

        var dbContext = GetDbContext();
        var accessor = new StubCurrentUserAccessor(UserHelpers.DefaultUserName);
        var handler = new Conduit.Features.Bookmarks.Add.QueryHandler(dbContext, accessor);

        var result = await handler.Handle(
            new Conduit.Features.Bookmarks.Add.Command(slug),
            CancellationToken.None
        );
        Assert.True(result.Article.Bookmarked);

        // calling it again must not create a duplicate row or throw
        var secondResult = await handler.Handle(
            new Conduit.Features.Bookmarks.Add.Command(slug),
            CancellationToken.None
        );
        Assert.True(secondResult.Article.Bookmarked);

        var bookmarkCount = await ExecuteDbContextAsync(db =>
            db.ArticleBookmarks.Where(b => b.Article!.Slug == slug).CountAsync()
        );
        Assert.Equal(1, bookmarkCount);
    }

    [Fact]
    public async Task Expect_Bookmark_Add_Does_Not_Affect_Favorited_Or_FavoritesCount()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Bookmark add does not favorite",
                Description = "Description",
                Body = "Body",
            }
        );
        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug!;

        var dbContext = GetDbContext();
        var accessor = new StubCurrentUserAccessor(UserHelpers.DefaultUserName);
        var handler = new Conduit.Features.Bookmarks.Add.QueryHandler(dbContext, accessor);

        var result = await handler.Handle(
            new Conduit.Features.Bookmarks.Add.Command(slug),
            CancellationToken.None
        );

        Assert.False(result.Article.Favorited);
        Assert.Equal(0, result.Article.FavoritesCount);
    }
}
