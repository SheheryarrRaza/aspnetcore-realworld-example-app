using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Articles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Bookmarks;

public class Add
{
    public record Command(string Slug) : IRequest<ArticleEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() => RuleFor(x => x.Slug).NotNull().NotEmpty();
    }

    public class Handler(ConduitContext context, ICurrentUserAccessor currentUserAccessor)
        : IRequestHandler<Command, ArticleEnvelope>
    {
        /// <summary>
        /// Bookmarks the article for the current user; repeating the call changes nothing.
        /// </summary>
        public async ValueTask<ArticleEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var currentPersonId = currentUserAccessor.GetCurrentPersonId();
            var article =
                await context
                    .Articles.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Slug == message.Slug, cancellationToken)
                ?? throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);

            var personExists = await context.Persons.AnyAsync(
                x => x.PersonId == currentPersonId,
                cancellationToken
            );
            if (!personExists || currentPersonId is null)
            {
                throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
            }

            var bookmarked = await context.ArticleBookmarks.AnyAsync(
                x => x.ArticleId == article.ArticleId && x.PersonId == currentPersonId,
                cancellationToken
            );
            if (!bookmarked)
            {
                await context.ArticleBookmarks.AddAsync(
                    new ArticleBookmark
                    {
                        ArticleId = article.ArticleId,
                        PersonId = currentPersonId.Value,
                        CreatedAt = DateTime.UtcNow,
                    },
                    cancellationToken
                );
                await context.SaveChangesAsync(cancellationToken);
            }

            return await BookmarkResponse.BuildAsync(
                context,
                article.ArticleId,
                currentPersonId,
                cancellationToken
            );
        }
    }
}
