using System.Linq;
using Conduit.Domain;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Articles;

public static class ArticleExtensions
{
    /// <summary>
    /// Loads articles with the related data needed for single-article responses.
    /// </summary>
    public static IQueryable<Article> GetAllData(this DbSet<Article> articles) =>
        articles
            .Include(x => x.Author)
            .Include(x => x.ArticleFavorites)
            .Include(x => x.ArticleTags)
            .AsNoTracking();

    /// <summary>
    /// Projects articles to list items, including the viewer-specific flags for the given person.
    /// </summary>
    public static IQueryable<ArticleSummary> ToSummaries(
        this IQueryable<Article> articles,
        ConduitContext context,
        int? currentPersonId
    ) =>
        articles.Select(x => new ArticleSummary
        {
            Slug = x.Slug,
            Title = x.Title,
            Description = x.Description,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            TagList = x.ArticleTags.Select(y => y.TagId!).ToList(),
            FavoritesCount = x.ArticleFavorites.Count,
            Favorited =
                currentPersonId != null
                && x.ArticleFavorites.Any(y => y.PersonId == currentPersonId),
            Bookmarked =
                currentPersonId != null
                && x.ArticleBookmarks.Any(y => y.PersonId == currentPersonId),
            Author =
                x.Author == null
                    ? null
                    : new Profile
                    {
                        Username = x.Author.Username,
                        Bio = x.Author.Bio,
                        Image = x.Author.Image,
                        IsFollowed =
                            currentPersonId != null
                            && context.FollowedPeople.Any(y =>
                                y.ObserverId == currentPersonId && y.TargetId == x.Author.PersonId
                            ),
                    },
        });
}
