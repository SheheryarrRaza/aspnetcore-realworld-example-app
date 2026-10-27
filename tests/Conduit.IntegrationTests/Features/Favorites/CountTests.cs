using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Favorites;
using Xunit;

namespace Conduit.IntegrationTests.Features.Favorites;

public class CountTests : SliceFixture
{
    private async Task<(Person Fan, Person Other, Article First, Article Second)> Seed()
    {
        var db = GetDbContext();
        var author = new Person { Username = "author" };
        var fan = new Person { Username = "fan" };
        var other = new Person { Username = "other" };
        var first = new Article { Slug = "first", Author = author };
        var second = new Article { Slug = "second", Author = author };
        db.AddRange(first, second, fan, other);
        await db.SaveChangesAsync();
        return (fan, other, first, second);
    }

    private async Task<int> CountFor(int personId) =>
        (
            await new Count.QueryHandler(GetDbContext()).Handle(new Count.Query(personId), default)
        ).FavoritesCount;

    [Fact]
    public async Task No_Favorites_Counts_Zero()
    {
        var (fan, _, _, _) = await Seed();
        Assert.Equal(0, await CountFor(fan.PersonId));
    }

    [Fact]
    public async Task Counts_Only_The_Callers_Favorites()
    {
        var (fan, other, first, second) = await Seed();
        var db = GetDbContext();
        db.Add(new ArticleFavorite { Article = first, Person = fan });
        db.Add(new ArticleFavorite { Article = second, Person = fan });
        db.Add(new ArticleFavorite { Article = first, Person = other });
        await db.SaveChangesAsync();

        Assert.Equal(2, await CountFor(fan.PersonId));
        Assert.Equal(1, await CountFor(other.PersonId));
    }

    [Fact]
    public async Task Add_Raises_And_Delete_Lowers_The_Count()
    {
        var (fan, _, _, _) = await Seed();
        var accessor = new StubCurrentUserAccessor("fan", fan.PersonId);

        await new Add.QueryHandler(GetDbContext(), accessor).Handle(
            new Add.Command("first"),
            default
        );
        await new Add.QueryHandler(GetDbContext(), accessor).Handle(
            new Add.Command("second"),
            default
        );
        Assert.Equal(2, await CountFor(fan.PersonId));

        await new Delete.QueryHandler(GetDbContext(), accessor).Handle(
            new Delete.Command("first"),
            default
        );
        Assert.Equal(1, await CountFor(fan.PersonId));
    }

    [Fact]
    public async Task Repeated_Add_Counts_Once()
    {
        var (fan, _, _, _) = await Seed();
        var accessor = new StubCurrentUserAccessor("fan", fan.PersonId);

        await new Add.QueryHandler(GetDbContext(), accessor).Handle(
            new Add.Command("first"),
            default
        );
        await new Add.QueryHandler(GetDbContext(), accessor).Handle(
            new Add.Command("first"),
            default
        );

        Assert.Equal(1, await CountFor(fan.PersonId));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Validator_Requires_A_Positive_PersonId(int personId, bool expected) =>
        Assert.Equal(
            expected,
            new Count.QueryValidator().Validate(new Count.Query(personId)).IsValid
        );
}
