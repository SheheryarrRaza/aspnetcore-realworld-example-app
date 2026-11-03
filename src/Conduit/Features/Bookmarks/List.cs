using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Bookmarks;

public class List
{
    public record Query(int? Limit, int? Offset) : IRequest<ArticlesEnvelope>, IReadOnlyRequest;

    public class QueryValidator : AbstractValidator<Query>
    {
        public QueryValidator()
        {
            RuleFor(x => x.Limit).GreaterThanOrEqualTo(0).When(x => x.Limit.HasValue);
            RuleFor(x => x.Offset).GreaterThanOrEqualTo(0).When(x => x.Offset.HasValue);
        }
    }

    public class QueryHandler(ConduitContext context, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Query, ArticlesEnvelope>
    {
        /// <summary>
        /// Lists the current user's bookmarked articles, most recently bookmarked first.
        /// </summary>
        public async ValueTask<ArticlesEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var currentPersonId =
                currentUserAccessor.GetCurrentPersonId()
                ?? throw new RestException(HttpStatusCode.Unauthorized, "token", "is missing");

            var bookmarks = context
                .ArticleBookmarks.AsNoTracking()
                .Where(x => x.PersonId == currentPersonId);

            var count = await bookmarks.CountAsync(cancellationToken);
            var articles = await bookmarks
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.ArticleId)
                .Skip(message.Offset ?? 0)
                .Take(message.Limit ?? 20)
                .Select(x => x.Article!)
                .ToSummaries(context, currentPersonId)
                .ToListAsync(cancellationToken);
            return new ArticlesEnvelope { Articles = articles, ArticlesCount = count };
        }
    }
}
