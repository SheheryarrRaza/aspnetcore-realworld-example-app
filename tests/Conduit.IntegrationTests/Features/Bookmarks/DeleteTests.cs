using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.IntegrationTests.Features.Articles;
using Conduit.IntegrationTests.Features.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conduit.IntegrationTests.Features.Bookmarks;

public class DeleteTests : SliceFixture
{
    [Fact]
    public async Task Expect_Bookmark_Delete_Clears_Bookmarked()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Bookmark delete article",
                Description = "Description",
                Body = "Body",
            }
        );
        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug!;

        var dbContext = GetDbContext();
        var accessor = new StubCurrentUserAccessor(UserHelpers.DefaultUserName);

        await new Conduit.Features.Bookmarks.Add.QueryHandler(dbContext, accessor).Handle(
            new Conduit.Features.Bookmarks.Add.Command(slug),
            CancellationToken.None
        );

        var result = await new Conduit.Features.Bookmarks.Delete.QueryHandler(
            dbContext,
            accessor
        ).Handle(new Conduit.Features.Bookmarks.Delete.Command(slug), CancellationToken.None);

        Assert.False(result.Article.Bookmarked);

        var bookmarkCount = await ExecuteDbContextAsync(db =>
            db.ArticleBookmarks.Where(b => b.Article!.Slug == slug).CountAsync()
        );
        Assert.Equal(0, bookmarkCount);
    }

    [Fact]
    public async Task Expect_Bookmark_Delete_On_Never_Bookmarked_Article_Is_NoOp()
    {
        var createCmd = new Create.Command(
            new Create.ArticleData
            {
                Title = "Bookmark delete noop article",
                Description = "Description",
                Body = "Body",
            }
        );
        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug!;

        var dbContext = GetDbContext();
        var accessor = new StubCurrentUserAccessor(UserHelpers.DefaultUserName);

        var result = await new Conduit.Features.Bookmarks.Delete.QueryHandler(
            dbContext,
            accessor
        ).Handle(new Conduit.Features.Bookmarks.Delete.Command(slug), CancellationToken.None);

        Assert.False(result.Article.Bookmarked);

        var bookmarkCount = await ExecuteDbContextAsync(db =>
            db.ArticleBookmarks.Where(b => b.Article!.Slug == slug).CountAsync()
        );
        Assert.Equal(0, bookmarkCount);
    }
}
