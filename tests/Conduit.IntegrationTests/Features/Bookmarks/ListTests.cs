using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Bookmarks;
using Conduit.Infrastructure.Errors;
using Xunit;
using ArticlesEnvelope = Conduit.Features.Articles.ArticlesEnvelope;

namespace Conduit.IntegrationTests.Features.Bookmarks;

public class ListTests : SliceFixture
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private async Task<(Person Reader, Person Other, Article[] Articles)> SeedAsync(
        int articleCount = 3
    )
    {
        var db = GetDbContext();
        var author = new Person { Username = "author" };
        var reader = new Person { Username = "reader" };
        var other = new Person { Username = "other" };
        // article n is created n days after Start, so article order differs from bookmark order
        var articles = Enumerable
            .Range(1, articleCount)
            .Select(n => new Article
            {
                Slug = $"article-{n}",
                Author = author,
                CreatedAt = Start.AddDays(n),
            })
            .ToArray();
        db.AddRange(articles);
        db.AddRange(reader, other);
        await db.SaveChangesAsync();
        return (reader, other, articles);
    }

    private static ArticleBookmark Bookmark(Person person, Article article, int minutes) =>
        new()
        {
            ArticleId = article.ArticleId,
            PersonId = person.PersonId,
            CreatedAt = Start.AddMinutes(minutes),
        };

    // bookmarks the articles one minute apart, so the last article is the newest bookmark
    private Task BookmarkInOrderAsync(Person person, Article[] articles) =>
        InsertAsync([.. articles.Select((article, index) => Bookmark(person, article, index))]);

    private async Task<ArticlesEnvelope> ListAsync(
        Person person,
        int? limit = null,
        int? offset = null
    ) =>
        await new List.QueryHandler(
            GetDbContext(),
            new StubCurrentUserAccessor(person.Username, person.PersonId)
        ).Handle(new List.Query(limit, offset), default);

    [Fact]
    public async Task Expect_Newest_Bookmark_First_Regardless_Of_Article_Date()
    {
        var (reader, _, articles) = await SeedAsync();
        await InsertAsync(
            Bookmark(reader, articles[2], 1),
            Bookmark(reader, articles[0], 3),
            Bookmark(reader, articles[1], 2)
        );

        var result = await ListAsync(reader);

        Assert.Equal(["article-1", "article-2", "article-3"], result.Articles.Select(x => x.Slug));
        Assert.All(result.Articles, x => Assert.True(x.Bookmarked));
        Assert.Equal(3, result.ArticlesCount);
    }

    [Fact]
    public async Task Expect_Limit_And_Offset_To_Page_The_List()
    {
        var (reader, _, articles) = await SeedAsync(5);
        await BookmarkInOrderAsync(reader, articles);

        var page = await ListAsync(reader, limit: 2, offset: 1);

        // bookmarked order is article-5, 4, 3, 2, 1
        Assert.Equal(["article-4", "article-3"], page.Articles.Select(x => x.Slug));
        Assert.Equal(5, page.ArticlesCount);
        Assert.Empty((await ListAsync(reader, limit: 0)).Articles);
        Assert.Empty((await ListAsync(reader, offset: 5)).Articles);
    }

    [Fact]
    public async Task Expect_Default_Limit_To_Be_Twenty()
    {
        var (reader, _, articles) = await SeedAsync(22);
        await BookmarkInOrderAsync(reader, articles);

        var result = await ListAsync(reader);

        Assert.Equal(20, result.Articles.Count);
        Assert.Equal(22, result.ArticlesCount);
    }

    [Fact]
    public async Task Expect_Only_The_Callers_Bookmarks()
    {
        var (reader, other, articles) = await SeedAsync();
        await InsertAsync(Bookmark(reader, articles[0], 1), Bookmark(other, articles[1], 2));

        var readers = await ListAsync(reader);
        var others = await ListAsync(other);

        Assert.Equal("article-1", Assert.Single(readers.Articles).Slug);
        Assert.Equal(1, readers.ArticlesCount);
        Assert.Equal("article-2", Assert.Single(others.Articles).Slug);
    }

    [Fact]
    public async Task Expect_Empty_List_Without_Bookmarks()
    {
        var (reader, _, _) = await SeedAsync();

        var result = await ListAsync(reader);

        Assert.Empty(result.Articles);
        Assert.Equal(0, result.ArticlesCount);
    }

    [Fact]
    public async Task Expect_Anonymous_Caller_To_Be_Unauthorized()
    {
        await SeedAsync();
        var handler = new List.QueryHandler(GetDbContext(), new StubCurrentUserAccessor(null));

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await handler.Handle(new List.Query(null, null), default)
        );

        Assert.Equal(HttpStatusCode.Unauthorized, exception.Code);
    }

    [Theory]
    [InlineData(-1, null, false)]
    [InlineData(null, -1, false)]
    [InlineData(0, 0, true)]
    [InlineData(null, null, true)]
    public void Expect_Validator_To_Reject_Negative_Paging(int? limit, int? offset, bool valid)
    {
        var result = new List.QueryValidator().Validate(new List.Query(limit, offset));

        Assert.Equal(valid, result.IsValid);
    }
}
