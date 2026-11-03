using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Favorites;
using Conduit.Features.Profiles;
using Conduit.Infrastructure.Errors;
using Xunit;

namespace Conduit.IntegrationTests.Features.Favorites;

public class ClearTests : SliceFixture
{
    private static async Task<int> FavoritesCountAsync(
        Counts.QueryHandler handler,
        string username
    ) => (await handler.Handle(new Counts.Query(username), default)).Counts.FavoritesCount;

    [Fact]
    public async Task Clear_Removes_Only_The_Callers_Favourites_And_Resets_The_Count()
    {
        var db = GetDbContext();
        var owner = new Person { Username = "owner" };
        var other = new Person { Username = "other" };
        var article = new Article { Slug = "article", Author = other };
        db.AddRange(owner, other, article);
        db.Add(new ArticleFavorite { Article = article, Person = owner });
        db.Add(new ArticleFavorite { Article = article, Person = other });
        await db.SaveChangesAsync();
        var cache = new ProfileCountsCache();
        var counts = new Counts.QueryHandler(db, cache);
        Assert.Equal(1, await FavoritesCountAsync(counts, "owner"));
        var handler = new Clear.CommandHandler(
            db,
            new StubCurrentUserAccessor("owner", owner.PersonId),
            cache
        );

        await handler.Handle(new Clear.Command(), default);
        await handler.Handle(new Clear.Command(), default);

        var remaining = Assert.Single(db.ArticleFavorites.ToList());
        Assert.Equal(other.PersonId, remaining.PersonId);
        Assert.Equal(0, await FavoritesCountAsync(counts, "owner"));
        Assert.Equal(1, await FavoritesCountAsync(counts, "other"));
    }

    [Fact]
    public async Task Clear_Without_A_Signed_In_User_Is_Unauthorized()
    {
        var handler = new Clear.CommandHandler(
            GetDbContext(),
            new StubCurrentUserAccessor(null),
            new ProfileCountsCache()
        );

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await handler.Handle(new Clear.Command(), default)
        );

        Assert.Equal(HttpStatusCode.Unauthorized, exception.Code);
    }
}
