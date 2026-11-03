using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Bookmarks;

internal static class BookmarkResponse
{
    /// <summary>
    /// Loads the article as the given person sees it, with favorited and bookmarked populated.
    /// </summary>
    public static async Task<ArticleEnvelope> BuildAsync(
        ConduitContext context,
        int articleId,
        int? personId,
        CancellationToken cancellationToken
    )
    {
        var article =
            await context
                .Articles.GetAllData()
                .FirstOrDefaultAsync(x => x.ArticleId == articleId, cancellationToken)
            ?? throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);

        var articles = new[] { article };
        articles.PopulateFavorited(personId);
        await articles.PopulateBookmarkedAsync(context, personId, cancellationToken);
        return new ArticleEnvelope(article);
    }
}
