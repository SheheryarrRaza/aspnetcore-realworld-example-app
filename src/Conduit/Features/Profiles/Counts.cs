using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Profiles;

public class Counts
{
    public record Query(string Username)
        : IRequest<ProfileCountsEnvelope>,
            Infrastructure.IReadOnlyRequest;

    public class QueryValidator : AbstractValidator<Query>
    {
        public QueryValidator() => RuleFor(x => x.Username).NotEmpty();
    }

    public class QueryHandler(ConduitContext context, ProfileCountsCache cache)
        : IRequestHandler<Query, ProfileCountsEnvelope>
    {
        /// <summary>
        /// Returns how many articles the user has favourited, from the cache when it is still fresh.
        /// </summary>
        public async ValueTask<ProfileCountsEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var personId =
                await context
                    .Persons.AsNoTracking()
                    .Where(x => x.Username == message.Username)
                    .Select(x => (int?)x.PersonId)
                    .FirstOrDefaultAsync(cancellationToken)
                ?? throw new RestException(HttpStatusCode.NotFound, "profile", Constants.NOT_FOUND);

            var favoritesCount = cache.GetFavoritesCount(personId);
            if (favoritesCount is null)
            {
                favoritesCount = await context.ArticleFavorites.CountAsync(
                    x => x.PersonId == personId,
                    cancellationToken
                );
                cache.SetFavoritesCount(personId, favoritesCount.Value);
            }

            return new ProfileCountsEnvelope(new ProfileCounts(favoritesCount.Value, null));
        }
    }
}
