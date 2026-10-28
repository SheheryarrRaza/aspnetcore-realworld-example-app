using System.Linq;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Favorites;
using FluentValidation;
using Xunit;

namespace Conduit.IntegrationTests.Features.Favorites;

public class CountTests : SliceFixture
{
    private async Task<(Person Fan, Person Other, Article First, Article Second)> SeedAsync()
    {
        var author = new Person { Username = "author" };
        var fan = new Person { Username = "fan" };
        var other = new Person { Username = "other" };
        var first = new Article { Slug = "first", Author = author };
        var second = new Article { Slug = "second", Author = author };
        await InsertAsync(first, second, fan, other);
        return (fan, other, first, second);
    }

    [Fact]
    public async Task Returns_Zero_When_User_Has_No_Favorites()
    {
        var (fan, _, _, _) = await SeedAsync();

        var result = await SendAsync(new Count.Query(fan.PersonId));

        Assert.Equal(0, result.FavoritesCount);
    }

    [Fact]
    public async Task Counts_Only_The_Current_Users_Favorites()
    {
        var (fan, other, first, second) = await SeedAsync();
        await InsertAsync(
            new ArticleFavorite { ArticleId = first.ArticleId, PersonId = fan.PersonId },
            new ArticleFavorite { ArticleId = second.ArticleId, PersonId = fan.PersonId },
            new ArticleFavorite { ArticleId = first.ArticleId, PersonId = other.PersonId }
        );

        Assert.Equal(2, (await SendAsync(new Count.Query(fan.PersonId))).FavoritesCount);
        Assert.Equal(1, (await SendAsync(new Count.Query(other.PersonId))).FavoritesCount);
    }

    [Fact]
    public async Task Count_Decreases_After_Unfavoriting()
    {
        var (fan, _, first, second) = await SeedAsync();
        var accessor = new StubCurrentUserAccessor("fan", fan.PersonId);
        var db = GetDbContext();
        var add = new Add.QueryHandler(db, accessor);
        await add.Handle(new Add.Command(first.Slug!), default);
        await add.Handle(new Add.Command(second.Slug!), default);
        Assert.Equal(2, (await SendAsync(new Count.Query(fan.PersonId))).FavoritesCount);

        await new Delete.QueryHandler(db, accessor).Handle(
            new Delete.Command(first.Slug!),
            default
        );

        Assert.Equal(1, (await SendAsync(new Count.Query(fan.PersonId))).FavoritesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_Non_Positive_PersonId(int personId)
    {
        var validator = new Count.QueryValidator();

        var result = await validator.ValidateAsync(new Count.Query(personId));

        Assert.False(result.IsValid);
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new Count.Query(personId)).AsTask()
        );
    }
}
