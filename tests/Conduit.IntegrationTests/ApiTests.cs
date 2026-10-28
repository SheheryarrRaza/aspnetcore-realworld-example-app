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

    [Fact]
    public async Task Favorites_Count_Requires_Authentication_And_Counts_Current_User()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var unauthorized = await client.GetAsync("/api/user/favorites/count");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("errors", await unauthorized.Content.ReadAsStringAsync());

        using var register = await client.PostAsync(
            "/api/users",
            Json("""{"user":{"username":"fan","email":"fan@example.com","password":"password"}}""")
        );
        register.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add(
            "Authorization",
            "Token " + user.RootElement.GetProperty("user").GetProperty("token").GetString()
        );
        using var created = await client.PostAsync(
            "/api/articles",
            Json(
                """{"article":{"title":"a title","description":"description","body":"body","tagList":[]}}"""
            )
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var empty = await client.GetAsync("/api/user/favorites/count");
        empty.EnsureSuccessStatusCode();
        using var emptyBody = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyBody.RootElement.GetProperty("favoritesCount").GetInt32());

        using var favorited = await client.PostAsync("/api/articles/a-title/favorite", null);
        favorited.EnsureSuccessStatusCode();
        using var counted = await client.GetAsync("/api/user/favorites/count");
        counted.EnsureSuccessStatusCode();
        using var countedBody = JsonDocument.Parse(await counted.Content.ReadAsStringAsync());
        Assert.Equal(1, countedBody.RootElement.GetProperty("favoritesCount").GetInt32());
    }
}
