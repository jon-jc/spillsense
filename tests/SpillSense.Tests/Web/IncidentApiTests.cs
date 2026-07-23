using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpillSense.Tests.Web;

public class IncidentApiTests : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public IncidentApiTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<JsonElement> GetJson(string url)
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task Lists_all_incidents_with_paging_envelope()
    {
        var body = await GetJson("/api/incidents");

        Assert.Equal(4, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(4, body.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("county=King", 1)]
    [InlineData("category=CrudeOil", 1)]
    [InlineData("medium=MarineWater", 2)]
    [InlineData("source=Vehicle", 1)]
    [InlineData("status=Closed", 2)]
    [InlineData("region=Southwest", 1)]
    [InlineData("from=2025-01-01&to=2026-01-01", 2)]
    [InlineData("search=Mystery", 1)]
    [InlineData("minGallons=100", 2)]
    [InlineData("hasCoordinates=false", 1)]
    [InlineData("bbox=-123.5,46.8,-122.0,48.0", 2)]
    public async Task Filters_combine_and_narrow_results(string queryString, int expected)
    {
        var body = await GetJson($"/api/incidents?{queryString}");

        Assert.Equal(expected, body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Paging_and_quantity_sort_work_together()
    {
        var body = await GetJson("/api/incidents?sort=-quantity&pageSize=2");

        var items = body.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("ERTS-2025-000002", items[0].GetProperty("reportNumber").GetString());
        Assert.Equal(4, body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invalid_enum_value_returns_400_with_guidance()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/incidents?medium=Lava", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("MarineWater", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_bbox_returns_400()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/incidents?bbox=1,2,3", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Detail_returns_full_record_with_county_and_region()
    {
        var body = await GetJson("/api/incidents/ERTS-2025-000002");

        Assert.Equal("Thurston", body.GetProperty("county").GetString());
        Assert.Equal("Southwest", body.GetProperty("ecologyRegion").GetString());
        Assert.Equal(500, body.GetProperty("quantityGallons").GetDecimal());
        Assert.Equal("Budd Inlet", body.GetProperty("waterbodyName").GetString());
    }

    [Fact]
    public async Task Unknown_report_number_returns_404()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/incidents/ERTS-1999-000000", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GeoJson_returns_rfc7946_feature_collection()
    {
        var body = await GetJson("/api/incidents/geojson");

        Assert.Equal("FeatureCollection", body.GetProperty("type").GetString());
        var features = body.GetProperty("features");
        Assert.Equal(3, features.GetArrayLength()); // only incidents with coordinates

        var first = features[0];
        Assert.Equal("Feature", first.GetProperty("type").GetString());
        var geometry = first.GetProperty("geometry");
        Assert.Equal("Point", geometry.GetProperty("type").GetString());

        // RFC 7946: positions are [longitude, latitude].
        var coords = geometry.GetProperty("coordinates");
        Assert.True(coords[0].GetDouble() < 0, "longitude (negative in WA) must come first");
        Assert.InRange(coords[1].GetDouble(), 45.0, 49.5);
    }

    [Fact]
    public async Task Stats_summary_rolls_up_counts_and_volumes()
    {
        var body = await GetJson("/api/stats/summary");

        Assert.Equal(4, body.GetProperty("totalIncidents").GetInt32());
        Assert.Equal(620, body.GetProperty("totalGallons").GetDouble());
        Assert.Equal(390, body.GetProperty("recoveredGallons").GetDouble());
        Assert.Equal(3, body.GetProperty("withCoordinates").GetInt32());

        var marine = body.GetProperty("byMedium").EnumerateArray()
            .Single(b => b.GetProperty("key").GetString() == "MarineWater");
        Assert.Equal(2, marine.GetProperty("count").GetInt32());
        Assert.Equal(600, marine.GetProperty("gallons").GetDouble());
    }

    [Fact]
    public async Task Stats_summary_respects_filters()
    {
        var body = await GetJson("/api/stats/summary?county=King");

        Assert.Equal(1, body.GetProperty("totalIncidents").GetInt32());
        Assert.Equal(100, body.GetProperty("totalGallons").GetDouble());
    }

    [Fact]
    public async Task Trend_returns_chronological_months()
    {
        var body = await GetJson("/api/stats/trend");

        var months = body.EnumerateArray().Select(p => p.GetProperty("month").GetString()).ToList();
        Assert.Equal(["2024-11", "2025-03", "2025-06", "2026-01"], months);
    }

    [Fact]
    public async Task County_rollup_orders_by_incident_count()
    {
        var body = await GetJson("/api/stats/counties");

        Assert.Equal(3, body.GetArrayLength());
        var first = body[0];
        Assert.Equal(1, first.GetProperty("count").GetInt32());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("fipsCode").GetString()));
    }

    [Fact]
    public async Task Import_runs_and_quarantine_are_exposed()
    {
        var runs = await GetJson("/api/imports");
        Assert.Equal(1, runs.GetArrayLength());
        var runId = runs[0].GetProperty("id").GetInt32();
        Assert.Equal("CompletedWithRejects", runs[0].GetProperty("status").GetString());

        var quarantine = await GetJson($"/api/imports/{runId}/quarantine");
        Assert.Equal(1, quarantine.GetArrayLength());
        Assert.Contains("not a recognizable date",
            quarantine[0].GetProperty("reasons")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quarantine_for_unknown_run_returns_404()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/imports/9999/quarantine", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SpillSense API", body, StringComparison.Ordinal);
    }
}
