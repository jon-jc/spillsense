using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SpillSense.Tests.Web;

/// <summary>
/// Upload tests mutate the database, so they take their own fixture instance
/// (one per test class) and never share state with the read-only API tests.
/// </summary>
public class ImportUploadApiTests : IClassFixture<ApiFixture>
{
    private const string Header =
        "ReportNumber,ReportedAt,OccurredAt,Description,Latitude,Longitude," +
        "LocationDescription,Waterbody,County,Medium,SubstanceName," +
        "QuantityGallons,RecoveredGallons,SourceType,ResponsibleParty,Status";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;

    public ImportUploadApiTests(ApiFixture fixture) => _fixture = fixture;

    private static MultipartFormDataContent Upload(string csv, string fileName = "upload.csv")
    {
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(csv)));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    private static async Task<JsonElement> PostCsv(HttpClient client, string csv, string fileName = "upload.csv")
    {
        using var content = Upload(csv, fileName);
        using var response = await client.PostAsync(new Uri("/api/imports", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task Uploading_a_csv_runs_the_intake_pipeline()
    {
        using var client = _fixture.CreateClient();
        var csv = $"{Header}\n" +
            "ERTS-2026-770001,2026-03-04T10:00:00Z,,Uploaded incident.,47.05,-122.90," +
            "Budd Inlet,Budd Inlet,Thurston,Marine Water,Diesel fuel,15,,Vessel,Acme Marine,Reported\n" +
            "ERTS-2026-770002,2026-03-05T10:00:00Z,,Swapped coordinates.,-122.90,47.05," +
            ",,Thurston,Marine Water,Diesel fuel,5,,Vessel,,Reported";

        var run = await PostCsv(client, csv);

        Assert.Equal("CompletedWithRejects", run.GetProperty("status").GetString());
        Assert.Equal("upload.csv", run.GetProperty("sourceName").GetString());
        Assert.Equal(1, run.GetProperty("insertedCount").GetInt32());
        Assert.Equal(1, run.GetProperty("rejectedCount").GetInt32());

        // The good row is immediately queryable; the bad one is reviewable.
        var listed = await GetJson(client, "/api/incidents?search=ERTS-2026-770001");
        Assert.Equal(1, listed.GetProperty("total").GetInt32());

        var quarantine = await GetJson(client,
            $"/api/imports/{run.GetProperty("id").GetInt32()}/quarantine");
        Assert.Contains("outside Washington State bounds",
            quarantine[0].GetProperty("reasons")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uploading_the_same_csv_twice_inserts_nothing_the_second_time()
    {
        using var client = _fixture.CreateClient();
        var csv = $"{Header}\n" +
            "ERTS-2026-780001,2026-04-04T10:00:00Z,,Repeat upload.,47.05,-122.90," +
            "Budd Inlet,Budd Inlet,Thurston,Marine Water,Diesel fuel,15,,Vessel,Acme Marine,Reported";

        var first = await PostCsv(client, csv);
        var second = await PostCsv(client, csv);

        Assert.Equal(1, first.GetProperty("insertedCount").GetInt32());
        Assert.Equal(0, second.GetProperty("insertedCount").GetInt32());
        Assert.Equal(1, second.GetProperty("unchangedCount").GetInt32());
    }

    [Fact]
    public async Task Upload_records_a_failed_run_when_required_columns_are_missing()
    {
        using var client = _fixture.CreateClient();

        var run = await PostCsv(client, "Foo,Bar\n1,2", "wrong-shape.csv");

        Assert.Equal("Failed", run.GetProperty("status").GetString());
        Assert.Contains("Missing required column",
            run.GetProperty("failureReason").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data", "incidents.xlsx")]
    [InlineData("", "empty.csv")]
    public async Task Upload_rejects_unusable_files(string body, string fileName)
    {
        using var client = _fixture.CreateClient();
        using var content = Upload(body, fileName);

        using var response = await client.PostAsync(new Uri("/api/imports", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
