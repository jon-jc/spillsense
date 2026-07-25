using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpillSense.Tests.Web;

public class ApiDocumentationTests : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public ApiDocumentationTests(ApiFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/explorer")]   // Scalar interactive explorer
    [InlineData("/docs.html")]  // branded reference page
    [InlineData("/openapi.json")] // published static document
    [InlineData("/lib/scalar/standalone.js")] // vendored, so neither page needs a CDN
    public async Task Documentation_surfaces_are_served(string path)
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Explorer_page_loads_the_vendored_bundle_not_a_cdn()
    {
        using var client = _fixture.CreateClient();

        var html = await client.GetStringAsync(new Uri("/explorer", UriKind.Relative));

        Assert.Contains("/lib/scalar/standalone.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.jsdelivr.net", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enum_backed_query_parameters_document_their_accepted_values()
    {
        using var client = _fixture.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/openapi/v1.json", UriKind.Relative), Json);

        var parameters = document
            .GetProperty("paths").GetProperty("/api/incidents")
            .GetProperty("get").GetProperty("parameters");

        var medium = parameters.EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Medium");

        // These bind as strings so the query parser can report every bad value
        // at once; the document has to carry the accepted values instead.
        var values = medium.GetProperty("schema").GetProperty("enum")
            .EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Contains("MarineWater", values);
        Assert.Contains("Groundwater", values);
        Assert.False(string.IsNullOrWhiteSpace(medium.GetProperty("description").GetString()));

        var status = parameters.EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Status");
        Assert.Contains("CleanupInProgress", status.GetProperty("schema").GetProperty("enum")
            .EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task Published_document_matches_the_live_one()
    {
        using var client = _fixture.CreateClient();

        var live = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/openapi/v1.json", UriKind.Relative), Json);
        var published = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/openapi.json", UriKind.Relative), Json);

        // Guards against the committed copy going stale after an endpoint change.
        var livePaths = live.GetProperty("paths").EnumerateObject().Select(p => p.Name).Order();
        var publishedPaths = published.GetProperty("paths").EnumerateObject().Select(p => p.Name).Order();
        Assert.Equal(livePaths, publishedPaths);
    }
}
