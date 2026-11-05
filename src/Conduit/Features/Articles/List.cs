using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Articles;

public class List
{
    public record Query(
        string? Tag,
        string? Author,
        string? FavoritedUsername,
        int? Limit,
        int? Offset,
        bool IsFeed = false
    ) : IRequest<ArticlesEnvelope>, IReadOnlyRequest;

    public class QueryHandler(
        ConduitContext context,
        ICurrentUserAccessor currentUserAccessor,
        CommentCountCache commentCountCache
    ) : IRequestHandler<Query, ArticlesEnvelope>
    {
        public const int MaxLimit = 100;

        public async ValueTask<ArticlesEnvelope> Handle(
            Query message,
            CancellationToken cancellationToken
        )
        {
            var currentPersonId = currentUserAccessor.GetCurrentPersonId();
            var queryable = context.Articles.AsNoTracking();

            if (message.IsFeed)
            {
                if (currentPersonId == null)
                {
                    throw new RestException(HttpStatusCode.Unauthorized, "token", "is missing");
                }
                queryable = queryable.Where(x =>
                    x.Author != null
                    && context.FollowedPeople.Any(y =>
                        y.ObserverId == currentPersonId && y.TargetId == x.Author.PersonId
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(message.Tag))
            {
                queryable = queryable.Where(x => x.ArticleTags.Any(y => y.TagId == message.Tag));
            }

            if (!string.IsNullOrWhiteSpace(message.Author))
            {
                queryable = queryable.Where(x =>
                    x.Author != null && x.Author.Username == message.Author
                );
            }

            if (!string.IsNullOrWhiteSpace(message.FavoritedUsername))
            {
                queryable = queryable.Where(x =>
                    x.ArticleFavorites.Any(y =>
                        y.Person != null && y.Person.Username == message.FavoritedUsername
                    )
                );
            }

            var count = await queryable.CountAsync(cancellationToken);
            var articles = await queryable
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.ArticleId)
                .Skip(message.Offset ?? 0)
                .Take(int.Min(message.Limit ?? 20, MaxLimit))
                .Select(x => new
                {
                    x.ArticleId,
                    Summary = new ArticleSummary
                    {
                        Slug = x.Slug,
                        Title = x.Title,
                        Description = x.Description,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt,
                        TagList = x.ArticleTags.Select(y => y.TagId!).ToList(),
                        FavoritesCount = x.ArticleFavorites.Count,
                        Favorited =
                            currentPersonId != null
                            && x.ArticleFavorites.Any(y => y.PersonId == currentPersonId),
                        Author =
                            x.Author == null
                                ? null
                                : new Profile
                                {
                                    Username = x.Author.Username,
                                    Bio = x.Author.Bio,
                                    Image = x.Author.Image,
                                    IsFollowed =
                                        currentPersonId != null
                                        && context.FollowedPeople.Any(y =>
                                            y.ObserverId == currentPersonId
                                            && y.TargetId == x.Author.PersonId
                                        ),
                                },
                    },
                })
                .ToListAsync(cancellationToken);

            var commentCounts = await LoadCommentCountsAsync(
                [.. articles.Select(x => x.ArticleId).Distinct()],
                cancellationToken
            );
            foreach (var article in articles)
            {
                article.Summary.CommentsCount = commentCounts[article.ArticleId];
            }

            return new ArticlesEnvelope
            {
                Articles = [.. articles.Select(x => x.Summary)],
                ArticlesCount = count,
            };
        }

        private async Task<Dictionary<int, int>> LoadCommentCountsAsync(
            List<int> articleIds,
            CancellationToken cancellationToken
        )
        {
            var counts = new Dictionary<int, int>();
            var misses = new List<int>();
            foreach (var articleId in articleIds)
            {
                if (commentCountCache.TryGet(articleId, out var cached))
                {
                    counts[articleId] = cached;
                }
                else
                {
                    misses.Add(articleId);
                }
            }

            if (misses.Count == 0)
            {
                return counts;
            }

            var snapshot = commentCountCache.Snapshot();
            var queried = await context
                .Comments.AsNoTracking()
                .Where(x => misses.Contains(x.ArticleId))
                .GroupBy(x => x.ArticleId)
                .Select(g => new { ArticleId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var missCounts = misses.ToDictionary(id => id, _ => 0);
            foreach (var item in queried)
            {
                missCounts[item.ArticleId] = item.Count;
            }

            commentCountCache.Store(missCounts, snapshot);
            foreach (var (articleId, count) in missCounts)
            {
                counts[articleId] = count;
            }

            return counts;
        }
    }
}
