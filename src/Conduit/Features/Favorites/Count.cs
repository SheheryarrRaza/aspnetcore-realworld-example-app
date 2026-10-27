using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Favorites;

public class Count
{
    public record Query(int PersonId) : IRequest<FavoritesCountEnvelope>, IReadOnlyRequest;

    public class QueryValidator : AbstractValidator<Query>
    {
        public QueryValidator() => RuleFor(x => x.PersonId).GreaterThan(0);
    }

    public class QueryHandler(ConduitContext context)
        : IRequestHandler<Query, FavoritesCountEnvelope>
    {
        /// <summary>Counts the favorited articles of the person given in the query.</summary>
        public async ValueTask<FavoritesCountEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var favoritesCount = await context
                .ArticleFavorites.AsNoTracking()
                .CountAsync(x => x.PersonId == message.PersonId, cancellationToken);

            return new FavoritesCountEnvelope(favoritesCount);
        }
    }
}

public record FavoritesCountEnvelope(int FavoritesCount);
