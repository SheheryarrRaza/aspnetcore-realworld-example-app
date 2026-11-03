using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.Infrastructure.Security;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Conduit.Features.Bookmarks;

[EnableRateLimiting(BookmarkRateLimiting.PolicyName)]
public class BookmarksController(IMediator mediator) : Controller
{
    /// <summary>
    /// Bookmarks an article for the signed-in user.
    /// </summary>
    [HttpPost("articles/{slug}/bookmark")]
    [Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
    public ValueTask<ArticleEnvelope> BookmarkAdd(
        string slug,
        CancellationToken cancellationToken
    ) => mediator.Send(new Add.Command(slug), cancellationToken);

    /// <summary>
    /// Removes an article from the signed-in user's bookmarks.
    /// </summary>
    [HttpDelete("articles/{slug}/bookmark")]
    [Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
    public ValueTask<ArticleEnvelope> BookmarkDelete(
        string slug,
        CancellationToken cancellationToken
    ) => mediator.Send(new Delete.Command(slug), cancellationToken);

    /// <summary>
    /// Lists the signed-in user's bookmarked articles, newest bookmark first.
    /// </summary>
    [HttpGet("user/bookmarks")]
    [Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
    public ValueTask<ArticlesEnvelope> BookmarkList(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    ) => mediator.Send(new List.Query(limit, offset), cancellationToken);
}
