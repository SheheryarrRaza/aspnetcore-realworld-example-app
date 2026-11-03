using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Bookmarks;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conduit.IntegrationTests.Features.Bookmarks;

public class DeleteTests : SliceFixture
{
    private async Task<(Person Reader, Person Other, Article Article)> SeedAsync()
    {
        var db = GetDbContext();
        var reader = new Person { Username = "reader" };
        var other = new Person { Username = "other" };
        var article = new Article
        {
            Slug = "article",
            Author = new Person { Username = "author" },
        };
        db.AddRange(article, reader, other);
        await db.SaveChangesAsync();
        return (reader, other, article);
    }

    private Delete.Handler CreateHandler(Person person) =>
        new(GetDbContext(), new StubCurrentUserAccessor(person.Username, person.PersonId));

    [Fact]
    public async Task Expect_Bookmark_Is_Removed_Only_For_The_Caller()
    {
        var (reader, other, article) = await SeedAsync();
        await InsertAsync(
            new ArticleBookmark { ArticleId = article.ArticleId, PersonId = reader.PersonId },
            new ArticleBookmark { ArticleId = article.ArticleId, PersonId = other.PersonId }
        );

        var result = await CreateHandler(reader).Handle(new Delete.Command("article"), default);

        Assert.False(result.Article.Bookmarked);
        var remaining = await ExecuteDbContextAsync(db => db.ArticleBookmarks.SingleAsync());
        Assert.Equal(other.PersonId, remaining.PersonId);
    }

    [Fact]
    public async Task Expect_Removing_A_Missing_Bookmark_To_Be_A_No_Op()
    {
        var (reader, _, _) = await SeedAsync();

        var result = await CreateHandler(reader).Handle(new Delete.Command("article"), default);

        Assert.False(result.Article.Bookmarked);
        Assert.Empty(await ExecuteDbContextAsync(db => db.ArticleBookmarks.ToListAsync()));
    }

    [Fact]
    public async Task Expect_Unknown_Slug_To_Be_Not_Found()
    {
        var (reader, _, _) = await SeedAsync();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await CreateHandler(reader).Handle(new Delete.Command("missing"), default)
        );

        Assert.Equal(HttpStatusCode.NotFound, exception.Code);
    }

    [Fact]
    public void Expect_Validator_To_Reject_Empty_Slug()
    {
        var validator = new Delete.CommandValidator();

        Assert.False(validator.Validate(new Delete.Command("")).IsValid);
        Assert.True(validator.Validate(new Delete.Command("article")).IsValid);
    }
}
