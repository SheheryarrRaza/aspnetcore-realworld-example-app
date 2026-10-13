using System;

namespace Conduit.Domain;

public class ArticleBookmark
{
    public int ArticleId { get; init; }
    public Article? Article { get; init; }

    public int PersonId { get; init; }
    public Person? Person { get; init; }

    public DateTime CreatedAt { get; init; }
}
