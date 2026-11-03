using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Conduit.IntegrationTests;

public class ApiTests
{
    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Authentication_And_Rename_Work_Through_The_Http_Pipeline()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var unauthorized = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("errors", await unauthorized.Content.ReadAsStringAsync());

        using var register = await client.PostAsync(
            "/api/users",
            Json(
                """{"user":{"username":"before","email":"before@example.com","password":"password"}}"""
            )
        );
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var document = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("user").GetProperty("token").GetString();
        client.DefaultRequestHeaders.Add("Authorization", "Token " + token);
        using var rename = await client.PutAsync(
            "/api/user",
            Json("""{"user":{"username":"after"}}""")
        );
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        using var current = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Contains("after", await current.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"user\":null}")]
    [InlineData("{\"user\":{}}")]
    [InlineData("{")]
    public async Task Invalid_Registration_Uses_RealWorld_Validation_Errors(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/users", Json(body));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("errors").ValueKind);
    }

    [Fact]
    public async Task Route_Prefix_Is_Configurable_And_Swagger_Remains_Available()
    {
        using var factory = new ApiFactory("custom");
        using var client = factory.CreateClient();
        using var tags = await client.GetAsync("/custom/tags");
        Assert.Equal(HttpStatusCode.OK, tags.StatusCode);
        using var oldRoute = await client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("/custom/tags", await swagger.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Article_Lists_Omit_Bodies_And_Report_Unpaginated_Counts()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var register = await client.PostAsync(
            "/api/users",
            Json(
                """{"user":{"username":"author","email":"author@example.com","password":"password"}}"""
            )
        );
        register.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "Authorization",
            "Token " + user.RootElement.GetProperty("user").GetProperty("token").GetString()
        );
        for (var i = 0; i < 2; i++)
        {
            using var created = await client.PostAsync(
                "/api/articles",
                Json(
                    """{"article":{"title":"same title","description":"description","body":"body","tagList":["tag"]}}"""
                )
            );
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        using var response = await client.GetAsync("/api/articles?limit=1&offset=0");
        response.EnsureSuccessStatusCode();
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, list.RootElement.GetProperty("articlesCount").GetInt32());
        var article = Assert.Single(list.RootElement.GetProperty("articles").EnumerateArray());
        Assert.False(article.TryGetProperty("body", out _));
        Assert.Equal(0, article.GetProperty("favoritesCount").GetInt32());
        Assert.Equal(
            "tag",
            Assert.Single(article.GetProperty("tagList").EnumerateArray()).GetString()
        );
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username)
    {
        using var register = await client.PostAsync(
            "/api/users",
            Json(
                $$$"""{"user":{"username":"{{{username}}}","email":"{{{username}}}@example.com","password":"password"}}"""
            )
        );
        register.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        return user.RootElement.GetProperty("user").GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? token
    )
    {
        using var request = new HttpRequestMessage(method, url);
        if (token != null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Token " + token);
        }
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Bookmarks_Require_A_Token_And_Stay_Private()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var authorToken = await RegisterAsync(client, "author");
        var readerToken = await RegisterAsync(client, "reader");
        var otherToken = await RegisterAsync(client, "other");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/articles")
        {
            Content = Json(
                """{"article":{"title":"readable","description":"description","body":"body"}}"""
            ),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Token " + authorToken);
        using var created = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        foreach (
            var (method, url) in new[]
            {
                (HttpMethod.Post, "/api/articles/readable/bookmark"),
                (HttpMethod.Delete, "/api/articles/readable/bookmark"),
                (HttpMethod.Get, "/api/user/bookmarks"),
            }
        )
        {
            using var anonymous = await SendAsync(client, method, url, null);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using var invalid = await SendAsync(client, method, url, "invalid");
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        }

        using var unknown = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/articles/unknown/bookmark",
            readerToken
        );
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var unknownDelete = await SendAsync(
            client,
            HttpMethod.Delete,
            "/api/articles/unknown/bookmark",
            readerToken
        );
        Assert.Equal(HttpStatusCode.NotFound, unknownDelete.StatusCode);

        using var bookmarked = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/articles/readable/bookmark",
            readerToken
        );
        Assert.Equal(HttpStatusCode.OK, bookmarked.StatusCode);
        using (var envelope = JsonDocument.Parse(await bookmarked.Content.ReadAsStringAsync()))
        {
            var article = envelope.RootElement.GetProperty("article");
            Assert.True(article.GetProperty("bookmarked").GetBoolean());
            Assert.False(article.GetProperty("favorited").GetBoolean());
            Assert.Equal(0, article.GetProperty("favoritesCount").GetInt32());
        }

        using var list = await SendAsync(
            client,
            HttpMethod.Get,
            "/api/user/bookmarks",
            readerToken
        );
        list.EnsureSuccessStatusCode();
        using (var listed = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, listed.RootElement.GetProperty("articlesCount").GetInt32());
            var item = Assert.Single(listed.RootElement.GetProperty("articles").EnumerateArray());
            Assert.Equal("readable", item.GetProperty("slug").GetString());
            Assert.True(item.GetProperty("bookmarked").GetBoolean());
            Assert.Equal(0, item.GetProperty("favoritesCount").GetInt32());
        }

        async Task<JsonElement> GetArticleAsync(string? token)
        {
            using var response = await SendAsync(
                client,
                HttpMethod.Get,
                "/api/articles/readable",
                token
            );
            response.EnsureSuccessStatusCode();
            return JsonDocument
                .Parse(await response.Content.ReadAsStringAsync())
                .RootElement.GetProperty("article")
                .Clone();
        }

        Assert.True((await GetArticleAsync(readerToken)).GetProperty("bookmarked").GetBoolean());
        foreach (var token in new[] { authorToken, otherToken, null })
        {
            var article = await GetArticleAsync(token);
            Assert.False(article.GetProperty("bookmarked").GetBoolean());
            Assert.Equal(0, article.GetProperty("favoritesCount").GetInt32());
        }

        using var otherList = await SendAsync(
            client,
            HttpMethod.Get,
            "/api/user/bookmarks",
            otherToken
        );
        using var otherListed = JsonDocument.Parse(await otherList.Content.ReadAsStringAsync());
        Assert.Equal(0, otherListed.RootElement.GetProperty("articlesCount").GetInt32());
        Assert.Empty(otherListed.RootElement.GetProperty("articles").EnumerateArray());

        using var removed = await SendAsync(
            client,
            HttpMethod.Delete,
            "/api/articles/readable/bookmark",
            readerToken
        );
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.False((await GetArticleAsync(readerToken)).GetProperty("bookmarked").GetBoolean());
    }

    [Fact]
    public async Task Bookmark_Endpoints_Are_Rate_Limited_Per_User()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var readerToken = await RegisterAsync(client, "reader");
        var otherToken = await RegisterAsync(client, "other");

        for (var i = 0; i < 120; i++)
        {
            using var allowed = await SendAsync(
                client,
                HttpMethod.Get,
                "/api/user/bookmarks",
                readerToken
            );
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var limited = await SendAsync(
            client,
            HttpMethod.Delete,
            "/api/articles/missing/bookmark",
            readerToken
        );
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var otherUser = await SendAsync(
            client,
            HttpMethod.Get,
            "/api/user/bookmarks",
            otherToken
        );
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }
}
