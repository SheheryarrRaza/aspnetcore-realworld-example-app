using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.IntegrationTests.Features.Comments;
using Conduit.IntegrationTests.Features.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CommentCreate = Conduit.Features.Comments.Create;
using CommentDelete = Conduit.Features.Comments.Delete;

namespace Conduit.IntegrationTests.Features.Articles;

public class CommentsCountTests : SliceFixture
{
    [Fact]
    public async Task Article_Without_Comments_Shows_Zero()
    {
        await UserHelpers.CreateDefaultUser(this);
        var slug = await CreateArticleAsync("No comments");

        var list = await ListAsync();

        Assert.Equal(0, CountFor(list, slug));
    }

    [Fact]
    public async Task Count_Is_Fresh_After_Comment_Is_Added()
    {
        await UserHelpers.CreateDefaultUser(this);
        var slug = await CreateArticleAsync("Added comment");
        Assert.Equal(0, CountFor(await ListAsync(), slug));

        await AddCommentAsync(slug);

        Assert.Equal(1, CountFor(await ListAsync(), slug));
    }

    [Fact]
    public async Task Count_Is_Fresh_After_Comment_Is_Deleted()
    {
        await UserHelpers.CreateDefaultUser(this);
        var slug = await CreateArticleAsync("Deleted comment");
        var first = await AddCommentAsync(slug);
        await AddCommentAsync(slug);
        Assert.Equal(2, CountFor(await ListAsync(), slug));

        await new CommentDelete.QueryHandler(
            GetDbContext(),
            new StubCurrentUserAccessor(UserHelpers.DefaultUserName),
            CommentCountCache
        ).Handle(new CommentDelete.Command(slug, first.CommentId), CancellationToken.None);

        Assert.Equal(1, CountFor(await ListAsync(), slug));
    }

    [Fact]
    public async Task Cached_Count_Is_Served_Without_Recounting()
    {
        await UserHelpers.CreateDefaultUser(this);
        var slug = await CreateArticleAsync("Cached count");
        Assert.Equal(0, CountFor(await ListAsync(), slug));

        // Written directly, bypassing the comment handlers, so nothing evicts the cached count.
        await ExecuteDbContextAsync(async db =>
        {
            var article = await db.Articles.SingleAsync(x => x.Slug == slug);
            var author = await db.Persons.SingleAsync(x =>
                x.Username == UserHelpers.DefaultUserName
            );
            db.Comments.Add(
                new Comment
                {
                    ArticleId = article.ArticleId,
                    AuthorId = author.PersonId,
                    Body = "written directly",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        });

        Assert.Equal(0, CountFor(await ListAsync(), slug));
    }

    [Fact]
    public async Task Each_Article_Shows_Its_Own_Count_In_The_Same_List()
    {
        await UserHelpers.CreateDefaultUser(this);
        var busy = await CreateArticleAsync("Busy article");
        var quiet = await CreateArticleAsync("Quiet article");
        await AddCommentAsync(busy);
        await AddCommentAsync(busy);
        await AddCommentAsync(quiet);

        var list = await ListAsync();

        Assert.Equal(2, CountFor(list, busy));
        Assert.Equal(1, CountFor(list, quiet));
    }

    [Fact]
    public async Task Author_List_Shows_Per_Article_Counts()
    {
        await UserHelpers.CreateDefaultUser(this);
        var busy = await CreateArticleAsync("Author busy");
        var quiet = await CreateArticleAsync("Author quiet");
        await AddCommentAsync(busy);
        await AddCommentAsync(busy);
        await AddCommentAsync(quiet);

        var list = await ListAsync(
            new List.Query(null, UserHelpers.DefaultUserName, null, null, null)
        );

        Assert.Equal(2, list.Articles.Count);
        Assert.Equal(2, CountFor(list, busy));
        Assert.Equal(1, CountFor(list, quiet));
    }

    [Fact]
    public async Task Feed_Shows_Count_For_Followed_Author_Articles()
    {
        await UserHelpers.CreateDefaultUser(this);
        var slug = await CreateArticleAsync("Feed article");
        await AddCommentAsync(slug);
        var fan = new Person { Username = "fan", Email = "fan@example.com" };
        await InsertAsync(fan);
        var authorId = await ExecuteDbContextAsync(db =>
            db.Persons.Where(x => x.Username == UserHelpers.DefaultUserName)
                .Select(x => x.PersonId)
                .SingleAsync()
        );
        await InsertAsync(new FollowedPeople { ObserverId = fan.PersonId, TargetId = authorId });

        var feed = await new List.QueryHandler(
            GetDbContext(),
            new StubCurrentUserAccessor("fan", fan.PersonId),
            CommentCountCache
        ).Handle(
            new List.Query(null, null, null, null, null) { IsFeed = true },
            CancellationToken.None
        );

        Assert.Equal(1, CountFor(feed, slug));
    }

    [Fact]
    public void Cached_Count_Expires_After_Thirty_Seconds()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var cache = new CommentCountCache(clock);
        cache.Store(new Dictionary<int, int> { [1] = 5 }, cache.Snapshot());
        Assert.True(cache.TryGet(1, out var cached));
        Assert.Equal(5, cached);

        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.False(cache.TryGet(1, out _));
    }

    [Fact]
    public void Count_Read_Before_An_Eviction_Is_Not_Cached()
    {
        var cache = new CommentCountCache(new ManualTimeProvider(DateTimeOffset.UtcNow));
        var snapshot = cache.Snapshot();
        cache.Invalidate(1);

        cache.Store(new Dictionary<int, int> { [1] = 5 }, snapshot);

        Assert.False(cache.TryGet(1, out _));
    }

    private async Task<string> CreateArticleAsync(string title)
    {
        var handler = new Create.Handler(
            GetDbContext(),
            new StubCurrentUserAccessor(UserHelpers.DefaultUserName)
        );
        var created = await handler.Handle(
            new Create.Command(
                new Create.ArticleData
                {
                    Title = title,
                    Description = "Description",
                    Body = "Body",
                }
            ),
            CancellationToken.None
        );
        return created.Article.Slug ?? throw new InvalidOperationException();
    }

    private async Task<Domain.Comment> AddCommentAsync(string slug) =>
        await CommentHelpers.CreateComment(
            this,
            new CommentCreate.Command(
                new CommentCreate.Model(new CommentCreate.CommentData("a comment")),
                slug
            ),
            UserHelpers.DefaultUserName
        );

    private async Task<ArticlesEnvelope> ListAsync() =>
        await ListAsync(new List.Query(null, null, null, null, null));

    private async Task<ArticlesEnvelope> ListAsync(List.Query query) => await SendAsync(query);

    private static int CountFor(ArticlesEnvelope list, string slug) =>
        list.Articles.Single(x => x.Slug == slug).CommentsCount;

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
