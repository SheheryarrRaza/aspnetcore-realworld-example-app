using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Bookmarks;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conduit.IntegrationTests.Features.Bookmarks;

public class AddTests : SliceFixture
{
    private async Task<(Person Author, Person Reader, Article Article)> SeedAsync()
    {
        var db = GetDbContext();
        var author = new Person { Username = "author" };
        var reader = new Person { Username = "reader" };
        var article = new Article { Slug = "article", Author = author };
        db.AddRange(article, reader);
        await db.SaveChangesAsync();
        return (author, reader, article);
    }

    private Add.Handler CreateHandler(Person person) =>
        new(GetDbContext(), new StubCurrentUserAccessor(person.Username, person.PersonId));

    [Fact]
    public async Task Expect_Bookmark_Is_Created_For_The_Caller()
    {
        var (_, reader, article) = await SeedAsync();

        var result = await CreateHandler(reader).Handle(new Add.Command("article"), default);

        Assert.True(result.Article.Bookmarked);
        var bookmark = await ExecuteDbContextAsync(db => db.ArticleBookmarks.SingleAsync());
        Assert.Equal(article.ArticleId, bookmark.ArticleId);
        Assert.Equal(reader.PersonId, bookmark.PersonId);
        Assert.Equal(DateTimeKind.Utc, bookmark.CreatedAt.Kind);
        Assert.True(DateTime.UtcNow - bookmark.CreatedAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Expect_Repeated_Bookmark_To_Be_A_No_Op()
    {
        var (_, reader, _) = await SeedAsync();
        await CreateHandler(reader).Handle(new Add.Command("article"), default);
        var first = await ExecuteDbContextAsync(db => db.ArticleBookmarks.SingleAsync());

        var result = await CreateHandler(reader).Handle(new Add.Command("article"), default);

        Assert.True(result.Article.Bookmarked);
        var second = await ExecuteDbContextAsync(db => db.ArticleBookmarks.SingleAsync());
        Assert.Equal(first.CreatedAt, second.CreatedAt);
    }

    [Fact]
    public async Task Expect_Authors_To_Bookmark_Their_Own_Article()
    {
        var (author, _, _) = await SeedAsync();

        var result = await CreateHandler(author).Handle(new Add.Command("article"), default);

        Assert.True(result.Article.Bookmarked);
    }

    [Fact]
    public async Task Expect_Unknown_Slug_To_Be_Not_Found()
    {
        var (_, reader, _) = await SeedAsync();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await CreateHandler(reader).Handle(new Add.Command("missing"), default)
        );

        Assert.Equal(HttpStatusCode.NotFound, exception.Code);
        Assert.Empty(await ExecuteDbContextAsync(db => db.ArticleBookmarks.ToListAsync()));
    }

    [Fact]
    public async Task Expect_Unknown_Person_To_Be_Not_Found()
    {
        await SeedAsync();
        var handler = new Add.Handler(GetDbContext(), new StubCurrentUserAccessor("ghost", 999));

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
            await handler.Handle(new Add.Command("article"), default)
        );

        Assert.Equal(HttpStatusCode.NotFound, exception.Code);
    }

    [Fact]
    public void Expect_Validator_To_Reject_Empty_Slug()
    {
        var validator = new Add.CommandValidator();

        Assert.False(validator.Validate(new Add.Command("")).IsValid);
        Assert.True(validator.Validate(new Add.Command("article")).IsValid);
    }

    [Fact]
    public async Task Expect_Bookmark_Not_To_Change_Favorites()
    {
        var (_, reader, _) = await SeedAsync();

        var result = await CreateHandler(reader).Handle(new Add.Command("article"), default);

        Assert.False(result.Article.Favorited);
        Assert.Equal(0, result.Article.FavoritesCount);
        Assert.Empty(await ExecuteDbContextAsync(db => db.ArticleFavorites.ToListAsync()));
    }

    [Fact]
    public async Task Expect_Bookmark_Not_To_Change_Existing_Favorites()
    {
        var (author, reader, article) = await SeedAsync();
        await InsertAsync(
            new ArticleFavorite { ArticleId = article.ArticleId, PersonId = author.PersonId }
        );

        var result = await CreateHandler(reader).Handle(new Add.Command("article"), default);

        Assert.False(result.Article.Favorited);
        Assert.Equal(1, result.Article.FavoritesCount);
        Assert.Single(await ExecuteDbContextAsync(db => db.ArticleFavorites.ToListAsync()));
    }
}
