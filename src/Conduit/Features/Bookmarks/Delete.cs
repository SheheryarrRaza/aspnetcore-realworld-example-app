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

public class Delete
{
    public record Command(string Slug) : IRequest<ArticleEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Slug).NotNull().NotEmpty();
    }

    public class QueryHandler(ConduitContext context, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Command, ArticleEnvelope>
    {
        public async ValueTask<ArticleEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var article =
                await context.Articles.FirstOrDefaultAsync(
                    x => x.Slug == message.Slug,
                    cancellationToken
                )
                ?? throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);

            var person = await context.Persons.FirstOrDefaultAsync(
                x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
                cancellationToken
            );
            if (person is null)
            {
                throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
            }

            var bookmark = await context.ArticleBookmarks.FirstOrDefaultAsync(
                x => x.ArticleId == article.ArticleId && x.PersonId == person.PersonId,
                cancellationToken
            );

            if (bookmark != null)
            {
                context.ArticleBookmarks.Remove(bookmark);
                await context.SaveChangesAsync(cancellationToken);
            }

            article = await context
                .Articles.GetAllData()
                .FirstOrDefaultAsync(x => x.ArticleId == article.ArticleId, cancellationToken);
            if (article is null)
            {
                throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
            }

            new[] { article }.PopulateFavorited(currentUserAccessor.GetCurrentPersonId());
            new[] { article }.PopulateBookmarked(currentUserAccessor.GetCurrentPersonId());
            return new ArticleEnvelope(article);
        }
    }
}
