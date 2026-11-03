using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Conduit.IntegrationTests.Features.Profiles;

public class CountsApiTests
{
    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

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

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Add("Authorization", "Token " + token);
        }
        return request;
    }

    private static async Task<(int Favorites, JsonElement Bookmarks)> ReadCountsAsync(
        HttpClient client,
        string username,
        string? token = null
    )
    {
        using var response = await client.SendAsync(
            Request(HttpMethod.Get, $"/api/profiles/{username}/counts", token)
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var counts = document.RootElement.GetProperty("counts");
        return (
            counts.GetProperty("favoritesCount").GetInt32(),
            counts.GetProperty("bookmarksCount").Clone()
        );
    }

    [Fact]
    public async Task Unknown_Username_Returns_Not_Found()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/profiles/nobody/counts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clearing_Favourites_Requires_A_Token()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(
            Request(HttpMethod.Delete, "/api/user/favorites")
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Favourite_Counts_Follow_Favouriting_And_Clearing_Through_The_Http_Pipeline()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client, "fan");
        using var created = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/api/articles")
            {
                Headers = { { "Authorization", "Token " + token } },
                Content = Json(
                    """{"article":{"title":"title","description":"description","body":"body"}}"""
                ),
            }
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(0, (await ReadCountsAsync(client, "fan")).Favorites);

        using var favorited = await client.SendAsync(
            Request(HttpMethod.Post, "/api/articles/title/favorite", token)
        );
        favorited.EnsureSuccessStatusCode();

        var anonymous = await ReadCountsAsync(client, "fan");
        Assert.Equal(1, anonymous.Favorites);
        Assert.Equal(JsonValueKind.Null, anonymous.Bookmarks.ValueKind);

        using var cleared = await client.SendAsync(
            Request(HttpMethod.Delete, "/api/user/favorites", token)
        );
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal(0, (await ReadCountsAsync(client, "fan", token)).Favorites);

        using var list = await client.GetAsync("/api/articles?favorited=fan");
        using var articles = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(0, articles.RootElement.GetProperty("articlesCount").GetInt32());
    }
}
