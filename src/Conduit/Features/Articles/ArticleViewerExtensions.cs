using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Articles;

public static class ArticleViewerExtensions
{
    /// <summary>
    /// Sets whether each article is favorited by the given person.
    /// </summary>
    public static void PopulateFavorited(this IEnumerable<Article> articles, int? personId)
    {
        foreach (var article in articles)
        {
            article.Favorited =
                personId.HasValue
                && article.ArticleFavorites.Any(x => x.PersonId == personId.Value);
        }
    }

    /// <summary>
    /// Sets whether each article is bookmarked by the given person, leaving it false for anonymous callers.
    /// </summary>
    public static async Task PopulateBookmarkedAsync(
        this IReadOnlyCollection<Article> articles,
        ConduitContext context,
        int? personId,
        CancellationToken cancellationToken
    )
    {
        if (personId is null)
        {
            return;
        }

        var articleIds = articles.Select(x => x.ArticleId).ToArray();
        var bookmarkedIds = await context
            .ArticleBookmarks.Where(x =>
                x.PersonId == personId.Value && articleIds.Contains(x.ArticleId)
            )
            .Select(x => x.ArticleId)
            .ToHashSetAsync(cancellationToken);
        foreach (var article in articles)
        {
            article.Bookmarked = bookmarkedIds.Contains(article.ArticleId);
        }
    }
}
