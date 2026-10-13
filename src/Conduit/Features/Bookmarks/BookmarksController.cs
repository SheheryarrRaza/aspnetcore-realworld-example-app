using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.Infrastructure.Security;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conduit.Features.Bookmarks;

[Route("articles")]
public class BookmarksController(IMediator mediator) : Controller
{
    [HttpPost("{slug}/bookmark")]
    [Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
    public ValueTask<ArticleEnvelope> BookmarkAdd(
        string slug,
        CancellationToken cancellationToken
    ) => mediator.Send(new Add.Command(slug), cancellationToken);

    [HttpDelete("{slug}/bookmark")]
    [Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
    public ValueTask<ArticleEnvelope> BookmarkDelete(
        string slug,
        CancellationToken cancellationToken
    ) => mediator.Send(new Delete.Command(slug), cancellationToken);
}
