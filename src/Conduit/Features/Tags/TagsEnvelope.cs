using System.Collections.Generic;

namespace Conduit.Features.Tags;

public class TagsEnvelope
{
    public List<string> Tags { get; set; } = new();
    public List<TagCount> TagCounts { get; set; } = new();
}

public record TagCount(string Tag, int Count);
