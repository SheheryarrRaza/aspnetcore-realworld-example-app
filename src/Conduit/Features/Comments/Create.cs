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

namespace Conduit.Features.Comments;

public class Create
{
    public record CommentData(string? Body);

    public record Command(Model Model, string Slug) : IRequest<CommentEnvelope>;

    public record Model(CommentData Comment);

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator() =>
            RuleFor(x => x.Model.Comment.Body).NotEmpty().WithMessage(Constants.BLANK);
    }

    public class Handler(
        ConduitContext context,
        ICurrentUserAccessor currentUserAccessor,
        CommentCountCache commentCountCache
    ) : IRequestHandler<Command, CommentEnvelope>
    {
        public async ValueTask<CommentEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var article = await context
                .Articles.Include(x => x.Comments)
                .FirstOrDefaultAsync(x => x.Slug == message.Slug, cancellationToken);

            if (article == null)
            {
                throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
            }

            var author = await context.Persons.FirstAsync(
                x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
                cancellationToken
            );

            var comment = new Comment
            {
                Author = author,
                Body = message.Model.Comment.Body ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await context.Comments.AddAsync(comment, cancellationToken);

            article.Comments.Add(comment);

            await context.SaveChangesAsync(cancellationToken);
            // Commit here so the eviction happens after the new count is visible to readers.
            // The transaction behaviour's later commit is then a no-op.
            await context.CommitTransactionAsync(cancellationToken);
            commentCountCache.Invalidate(article.ArticleId);

            return new CommentEnvelope(comment);
        }
    }
}
