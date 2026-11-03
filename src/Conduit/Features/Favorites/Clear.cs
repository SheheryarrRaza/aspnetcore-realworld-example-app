using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Favorites;

public class Clear
{
    public record Command : IRequest;

    public class CommandHandler(
        ConduitContext context,
        ICurrentUserAccessor currentUserAccessor,
        ProfileCountsCache countsCache
    ) : IRequestHandler<Command>
    {
        /// <summary>
        /// Removes every favourite of the signed-in user and drops their cached counts.
        /// </summary>
        public async ValueTask<Unit> Handle(Command message, CancellationToken cancellationToken)
        {
            var personId =
                currentUserAccessor.GetCurrentPersonId()
                ?? throw new RestException(HttpStatusCode.Unauthorized, "token", "is missing");

            await context
                .ArticleFavorites.Where(x => x.PersonId == personId)
                .ExecuteDeleteAsync(cancellationToken);
            countsCache.Evict(personId);
            return Unit.Value;
        }
    }
}
