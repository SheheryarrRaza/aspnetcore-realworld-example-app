using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Tags;

public class List
{
    public record Query : IRequest<TagsEnvelope>, IReadOnlyRequest;

    public class QueryHandler(ConduitContext context) : IRequestHandler<Query, TagsEnvelope>
    {
        public async ValueTask<TagsEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var tags = await context
                .Tags.OrderBy(x => x.TagId)
                .AsNoTracking()
                .Select(x => new { Tag = x.TagId ?? string.Empty, Count = x.ArticleTags.Count })
                .ToListAsync(cancellationToken);
            return new TagsEnvelope
            {
                Tags = [.. tags.Select(x => x.Tag)],
                TagCounts =
                [
                    .. tags
                        .Where(x => x.Count > 0)
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Tag, System.StringComparer.Ordinal)
                        .Select(x => new TagCount(x.Tag, x.Count))
                ],
            };
        }
    }
}
