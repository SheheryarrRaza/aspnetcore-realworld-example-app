using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Conduit.IntegrationTests.Features.Tags;

public class ListTests
{
    private static readonly string[] m_initialTagCounts =
        ["shared:2", "alpha:1", "beta:1", "unused:1"];
    private static readonly string[] m_remainingTagCounts = ["shared:2", "alpha:1", "beta:1"];
    private static readonly string[] m_legacyTags = ["alpha", "beta", "shared", "unused"];

    /// <summary>Returns article counts in the tag list while preserving the legacy tags array.</summary>
    [Fact]
    public async Task Tags_Include_Ordered_Article_Counts_And_Keep_Legacy_Array()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var register = await client.PostAsync(
            "/api/users",
            Json(
                """{"user":{"username":"tag-author","email":"tag-author@example.com","password":"password"}}"""
            )
        );
        register.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "Authorization",
            "Token " + user.RootElement.GetProperty("user").GetProperty("token").GetString()
        );

        await CreateArticleAsync(client, "First", ["shared"]);
        var unusedTagArticle = await CreateArticleAsync(client, "Unused", ["unused"]);
        await CreateArticleAsync(client, "Second", ["shared"]);
        await CreateArticleAsync(client, "Alpha", ["alpha"]);
        await CreateArticleAsync(client, "Beta", ["beta"]);

        using var initialResponse = await client.GetAsync("/api/tags");
        initialResponse.EnsureSuccessStatusCode();
        using var initialTags = JsonDocument.Parse(await initialResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            m_initialTagCounts,
            ReadTagCounts(initialTags.RootElement)
        );
        Assert.Equal(
            m_legacyTags,
            ReadLegacyTags(initialTags.RootElement)
        );

        using var deleted = await client.DeleteAsync($"/api/articles/{unusedTagArticle}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var tagsResponse = await client.GetAsync("/api/tags");
        tagsResponse.EnsureSuccessStatusCode();
        using var tags = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            m_remainingTagCounts,
            ReadTagCounts(tags.RootElement)
        );
        Assert.Equal(
            m_legacyTags,
            ReadLegacyTags(tags.RootElement)
        );

        using var filteredResponse = await client.GetAsync("/api/articles?tag=shared");
        filteredResponse.EnsureSuccessStatusCode();
        using var filtered = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, filtered.RootElement.GetProperty("articlesCount").GetInt32());
    }

    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

    private static async Task<string> CreateArticleAsync(
        HttpClient client,
        string title,
        string[] tags
    )
    {
        var tagList = string.Join(",", tags.Select(tag => JsonSerializer.Serialize(tag)));
        using var response = await client.PostAsync(
            "/api/articles",
            Json(
                $"{{\"article\":{{\"title\":\"{title}\",\"description\":\"description\",\"body\":\"body\",\"tagList\":[{tagList}]}}}}"
            )
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var article = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return article.RootElement.GetProperty("article").GetProperty("slug").GetString()!;
    }

    private static string[] ReadTagCounts(JsonElement response) =>
    [
        .. response
            .GetProperty("tagCounts")
            .EnumerateArray()
            .Select(x => $"{x.GetProperty("tag").GetString()}:{x.GetProperty("count").GetInt32()}")
    ];

    private static string?[] ReadLegacyTags(JsonElement response) =>
    [.. response.GetProperty("tags").EnumerateArray().Select(x => x.GetString())];
}
