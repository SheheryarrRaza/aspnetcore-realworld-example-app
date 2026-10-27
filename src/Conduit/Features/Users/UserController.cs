using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Favorites;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Security;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conduit.Features.Users;

[Route("user")]
[Authorize(AuthenticationSchemes = JwtIssuerOptions.Schemes)]
public class UserController(IMediator mediator, ICurrentUserAccessor currentUserAccessor)
{
    [HttpGet]
    public ValueTask<UserEnvelope> GetCurrent(CancellationToken cancellationToken) =>
        mediator.Send(
            new Details.Query(currentUserAccessor.GetCurrentPersonId() ?? 0),
            cancellationToken
        );

    /// <summary>Returns how many articles the current user has favorited.</summary>
    [HttpGet("favorites/count")]
    public ValueTask<FavoritesCountEnvelope> GetFavoritesCount(
        CancellationToken cancellationToken
    ) =>
        mediator.Send(
            new Count.Query(currentUserAccessor.GetCurrentPersonId() ?? 0),
            cancellationToken
        );

    [HttpPut]
    public ValueTask<UserEnvelope> UpdateUser(
        [FromBody] Edit.Command command,
        CancellationToken cancellationToken
    ) => mediator.Send(command, cancellationToken);
}
