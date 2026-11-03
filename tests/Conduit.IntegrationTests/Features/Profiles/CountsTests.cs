using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Favorites;
using Conduit.Features.Profiles;
using Conduit.Infrastructure.Errors;
using Xunit;

namespace Conduit.IntegrationTests.Features.Profiles;

public class CountsTests : SliceFixture
{
    private static async Task<int> FavoritesCountAsync(
        Counts.QueryHandler handler,
        string username
    ) => (await handler.Handle(new Counts.Query(username), default)).Counts.FavoritesCount;

    private async Task<(Person Owner, Article First, Article Second)> SeedAsync()
    {
        var db = GetDbContext();
        var owner = new Person { Username = "owner" };
        var author = new Person { Username = "author" };
        var first = new Article { Slug = "first", Author = author };
        var second = new Article { Slug = "second", Author = author };
        db.AddRange(owner, author, first, second);
        db.Add(new ArticleFavorite { Article = first, Person = owner });
        db.Add(new ArticleFavorite { Article = second, Person = owner });
        db.Add(new ArticleFavorite { Article = first, Person = author });
        await db.SaveChangesAsync();
        return (owner, first, second);
    }

    [Fact]
    public async Task Counts_Only_Include_The_Favourites_Of_The_Requested_User()
    {
        await SeedAsync();
        var handler = new Counts.QueryHandler(GetDbContext(), new ProfileCountsCache());

        var owner = await handler.Handle(new Counts.Query("owner"), default);
        var author = await handler.Handle(new Counts.Query("author"), default);

        Assert.Equal(2, owner.Counts.FavoritesCount);
        Assert.Null(owner.Counts.BookmarksCount);
        Assert.Equal(1, author.Counts.FavoritesCount);
        Assert.Null(author.Counts.BookmarksCount);
    }

    [Fact]
    public async Task Unknown_Username_Is_Not_Found()
    {
        var handler = new Counts.QueryHandler(GetDbContext(), new ProfileCountsCache());

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await handler.Handle(new Counts.Query("nobody"), default)
        );

        Assert.Equal(HttpStatusCode.NotFound, exception.Code);
    }

    [Fact]
    public async Task Repeated_Requests_Use_The_Cache_Until_It_Is_Evicted()
    {
        var (owner, _, _) = await SeedAsync();
        var db = GetDbContext();
        var cache = new ProfileCountsCache();
        var handler = new Counts.QueryHandler(db, cache);
        Assert.Equal(2, await FavoritesCountAsync(handler, "owner"));

        db.ArticleFavorites.RemoveRange(
            db.ArticleFavorites.Where(x => x.PersonId == owner.PersonId)
        );
        await db.SaveChangesAsync();

        Assert.Equal(2, await FavoritesCountAsync(handler, "owner"));
        cache.Evict(owner.PersonId);
        Assert.Equal(0, await FavoritesCountAsync(handler, "owner"));
    }

    [Fact]
    public async Task Favouriting_And_Unfavouriting_Show_Up_Straight_Away()
    {
        var (owner, _, _) = await SeedAsync();
        var db = GetDbContext();
        var cache = new ProfileCountsCache();
        var accessor = new StubCurrentUserAccessor("owner", owner.PersonId);
        var counts = new Counts.QueryHandler(db, cache);
        Assert.Equal(2, await FavoritesCountAsync(counts, "owner"));

        await new Delete.QueryHandler(db, accessor, cache).Handle(
            new Delete.Command("first"),
            default
        );
        Assert.Equal(1, await FavoritesCountAsync(counts, "owner"));

        await new Add.QueryHandler(db, accessor, cache).Handle(new Add.Command("first"), default);
        Assert.Equal(2, await FavoritesCountAsync(counts, "owner"));
    }

    [Fact]
    public void Validator_Rejects_An_Empty_Username()
    {
        var validator = new Counts.QueryValidator();

        Assert.False(validator.Validate(new Counts.Query("")).IsValid);
        Assert.True(validator.Validate(new Counts.Query("owner")).IsValid);
    }
}
