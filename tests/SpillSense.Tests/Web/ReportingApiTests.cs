using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpillSense.Tests.Web;

public class ReportingApiTests : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public ReportingApiTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Csv_export_streams_filtered_rows_with_header()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/incidents/export?county=King", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.ToString(), StringComparison.Ordinal);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition!.ToString(), StringComparison.Ordinal);

        var lines = (await response.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("ReportNumber,ReportedAtUtc", lines[0], StringComparison.Ordinal);
        Assert.Equal(2, lines.Length); // header + the single King County seed incident
        Assert.Contains("ERTS-2025-000001", lines[1], StringComparison.Ordinal);
        Assert.Contains("Cascadia Marine Services LLC", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Csv_export_rejects_invalid_filters()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/incidents/export?medium=Lava", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Annual_report_composes_yearly_rollup()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/reports/annual/2025", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(2025, body.GetProperty("year").GetInt32());
        Assert.Equal(2, body.GetProperty("totalIncidents").GetInt32());
        Assert.Equal(600, body.GetProperty("totalGallons").GetDouble());
        Assert.Equal(390, body.GetProperty("recoveredGallons").GetDouble());
        Assert.Equal(65, body.GetProperty("recoveryRatePercent").GetDouble());

        // Seed data: one incident in Q1 (March), one in Q2 (June).
        var quarters = body.GetProperty("byQuarter").EnumerateArray()
            .ToDictionary(q => q.GetProperty("quarter").GetString()!, q => q.GetProperty("count").GetInt32());
        Assert.Equal(1, quarters["Q1"]);
        Assert.Equal(1, quarters["Q2"]);

        var largest = body.GetProperty("largestIncidents");
        Assert.Equal("ERTS-2025-000002", largest[0].GetProperty("reportNumber").GetString());

        // 2024 had one incident; 2025 has two -> +100%.
        Assert.Equal(1, body.GetProperty("previousYearIncidents").GetInt32());
        Assert.Equal(100, body.GetProperty("yearOverYearChangePercent").GetDouble());
    }

    [Fact]
    public async Task Annual_report_rejects_implausible_year()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/reports/annual/1889", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Annual_report_for_empty_year_returns_zeroes()
    {
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync(new Uri("/api/reports/annual/1995", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, body.GetProperty("totalIncidents").GetInt32());
        Assert.Equal(0, body.GetProperty("recoveryRatePercent").GetDouble());
        Assert.True(body.GetProperty("yearOverYearChangePercent").ValueKind == JsonValueKind.Null);
    }
}
