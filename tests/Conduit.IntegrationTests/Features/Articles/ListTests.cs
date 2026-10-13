using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.Infrastructure.Errors;
using Xunit;

namespace Conduit.IntegrationTests.Features.Articles;

public class ListTests : SliceFixture
{
    [Fact]
    public async Task Expect_List_Articles_No_Params()
    {
        var command = new Create.Command(
            new Create.ArticleData
            {
                Title = "Test article list",
                Description = "Description",
                Body = "Body",
                TagList = ["tag1"],
            }
        );
        await ArticleHelpers.CreateArticle(this, command);

        var result = await SendAsync(new List.Query(null, null, null, null, null));

        Assert.NotNull(result);
        Assert.True(result.ArticlesCount > 0);
    }

    [Fact]
    public async Task Expect_List_Articles_With_Tag_Filter()
    {
        var command = new Create.Command(
            new Create.ArticleData
            {
                Title = "Test article tag filter",
                Description = "Description",
                Body = "Body",
                TagList = ["uniquetag123"],
            }
        );
        await ArticleHelpers.CreateArticle(this, command);

        var result = await SendAsync(new List.Query("uniquetag123", null, null, null, null));

        Assert.NotNull(result);
        Assert.True(result.ArticlesCount > 0);
    }

    [Fact]
    public async Task Expect_List_Articles_With_Nonexistent_Tag_Returns_Empty()
    {
        var result = await SendAsync(new List.Query("nonexistenttag99999", null, null, null, null));

        Assert.NotNull(result);
        Assert.Equal(0, result.ArticlesCount);
    }

    [Fact]
    public async Task Expect_List_Articles_With_Limit_And_Offset()
    {
        var user = await Users.UserHelpers.CreateDefaultUser(this);
        var dbContext = GetDbContext();
        var currentAccessor = new StubCurrentUserAccessor(user.Username);
        var handler = new Create.Handler(dbContext, currentAccessor);
        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(
                new Create.Command(
                    new Create.ArticleData
                    {
                        Title = $"Test article pagination {i}",
                        Description = "Description",
                        Body = "Body",
                    }
                ),
                CancellationToken.None
            );
        }

        var result = await SendAsync(new List.Query(null, null, null, 2, 0));

        Assert.NotNull(result);
        Assert.Equal(2, result.Articles.Count);
    }

    [Fact]
    public async Task Expect_Feed_With_No_Params_Returns_Empty_For_User_With_No_Follows()
    {
        var user = await Users.UserHelpers.CreateDefaultUser(this);
        var dbContext = GetDbContext();
        var currentAccessor = new StubCurrentUserAccessor(user.Username);
        var handler = new List.QueryHandler(dbContext, currentAccessor);

        var result = await handler.Handle(
            new List.Query(null, null, null, null, null) { IsFeed = true },
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(0, result.ArticlesCount);
    }

    [Fact]
    public async Task Expect_Bookmarked_List_Returns_Only_Callers_Bookmarks_Newest_First_With_Paging()
    {
        var dbContext = GetDbContext();
        var author = new Person { Username = "bookmarklistauthor" };
        var caller = new Person { Username = "bookmarklistcaller" };
        var other = new Person { Username = "bookmarklistother" };
        var articleA = new Article
        {
            Slug = "bookmark-list-a",
            Title = "A",
            Author = author,
        };
        var articleB = new Article
        {
            Slug = "bookmark-list-b",
            Title = "B",
            Author = author,
        };
        var articleC = new Article
        {
            Slug = "bookmark-list-c",
            Title = "C",
            Author = author,
        };
        dbContext.AddRange(author, caller, other, articleA, articleB, articleC);
        await dbContext.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;
        dbContext.AddRange(
            new ArticleBookmark
            {
                Article = articleA,
                Person = caller,
                CreatedAt = baseTime,
            },
            new ArticleBookmark
            {
                Article = articleB,
                Person = caller,
                CreatedAt = baseTime.AddMinutes(1),
            },
            // bookmarked by a different user; must never appear in the caller's list
            new ArticleBookmark
            {
                Article = articleC,
                Person = other,
                CreatedAt = baseTime.AddMinutes(2),
            }
        );
        await dbContext.SaveChangesAsync();

        var accessor = new StubCurrentUserAccessor(caller.Username, caller.PersonId);
        var result = await new List.QueryHandler(dbContext, accessor).Handle(
            new List.Query(null, null, null, null, null) { IsBookmarked = true },
            CancellationToken.None
        );

        Assert.Equal(2, result.ArticlesCount);
        Assert.Collection(
            result.Articles,
            a => Assert.Equal(articleB.Slug, a.Slug),
            a => Assert.Equal(articleA.Slug, a.Slug)
        );

        var paged = await new List.QueryHandler(dbContext, accessor).Handle(
            new List.Query(null, null, null, 1, 1) { IsBookmarked = true },
            CancellationToken.None
        );
        Assert.Equal(2, paged.ArticlesCount);
        Assert.Equal(articleA.Slug, Assert.Single(paged.Articles).Slug);
    }

    [Fact]
    public async Task Expect_Bookmarked_List_Throws_Unauthorized_When_No_Current_User()
    {
        var accessor = new StubCurrentUserAccessor(null);

        var ex = await Assert.ThrowsAsync<RestException>(() =>
            new List.QueryHandler(GetDbContext(), accessor)
                .Handle(
                    new List.Query(null, null, null, null, null) { IsBookmarked = true },
                    CancellationToken.None
                )
                .AsTask()
        );
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, ex.Code);
    }
}
