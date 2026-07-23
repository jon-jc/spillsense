using Microsoft.Extensions.Logging.Abstractions;
using SpillSense.Domain.Intake;
using SpillSense.Infrastructure.Etl;

namespace SpillSense.Tests.Etl;

public class IncidentImportServiceTests
{
    private const string Header =
        "ReportNumber,ReportedAt,OccurredAt,Description,Latitude,Longitude," +
        "LocationDescription,Waterbody,County,Medium,SubstanceName," +
        "QuantityGallons,RecoveredGallons,SourceType,ResponsibleParty,Status";

    private static string Csv(params string[] rows) =>
        string.Join('\n', [Header, .. rows]);

    private static string Row(string reportNumber, string quantity = "12.0", string description = "Sheen near dock.") =>
        $"{reportNumber},2026-06-01T14:00:00Z,,{description},47.0605,-122.9007," +
        $"Budd Inlet,Budd Inlet,Thurston,Marine Water,Diesel fuel,{quantity},,Vessel,Acme Marine,Reported";

    private static IncidentImportService Service(TestDatabase db) =>
        new(db.Context, NullLogger<IncidentImportService>.Instance);

    private static async Task<ImportRun> Import(TestDatabase db, string csv, string name = "test.csv")
    {
        using var reader = new StringReader(csv);
        return await Service(db).ImportAsync(name, reader);
    }

    [Fact]
    public async Task Imports_valid_rows_and_records_run_stats()
    {
        using var db = new TestDatabase();

        var run = await Import(db, Csv(Row("ERTS-2026-000001"), Row("ERTS-2026-000002")));

        Assert.Equal(ImportRunStatus.Succeeded, run.Status);
        Assert.Equal(2, run.TotalRows);
        Assert.Equal(2, run.InsertedCount);
        Assert.Equal(0, run.RejectedCount);
        Assert.Equal(2, db.Context.Incidents.Count());
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public async Task Reimporting_the_same_file_changes_nothing()
    {
        using var db = new TestDatabase();
        var csv = Csv(Row("ERTS-2026-000001"), Row("ERTS-2026-000002"));

        await Import(db, csv);
        var second = await Import(db, csv);

        Assert.Equal(0, second.InsertedCount);
        Assert.Equal(0, second.UpdatedCount);
        Assert.Equal(2, second.UnchangedCount);
        Assert.Equal(2, db.Context.Incidents.Count());
    }

    [Fact]
    public async Task Changed_row_updates_the_existing_incident()
    {
        using var db = new TestDatabase();

        await Import(db, Csv(Row("ERTS-2026-000001", quantity: "12.0")));
        var second = await Import(db, Csv(Row("ERTS-2026-000001", quantity: "45.5")));

        Assert.Equal(1, second.UpdatedCount);
        var incident = db.Context.Incidents.Single();
        Assert.Equal(45.5m, incident.QuantityGallons);
    }

    [Fact]
    public async Task Invalid_rows_are_quarantined_with_raw_row_and_reasons()
    {
        using var db = new TestDatabase();
        var badRow = "ERTS-2026-000003,2026-06-01T14:00:00Z,,Bad coords.,-122.9,47.06," +
                     ",,Thurston,Marine Water,Diesel fuel,5,,Vessel,,Reported";

        var run = await Import(db, Csv(Row("ERTS-2026-000001"), badRow));

        Assert.Equal(ImportRunStatus.CompletedWithRejects, run.Status);
        Assert.Equal(1, run.InsertedCount);
        Assert.Equal(1, run.RejectedCount);

        var quarantined = db.Context.QuarantinedRecords.Single();
        Assert.Equal(2, quarantined.RowNumber);
        Assert.Equal("ERTS-2026-000003", quarantined.ReportNumber);
        Assert.Contains("outside Washington State bounds", quarantined.Reasons, StringComparison.Ordinal);
        Assert.Contains("Bad coords.", quarantined.RawRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_report_number_within_file_is_quarantined()
    {
        using var db = new TestDatabase();

        var run = await Import(db, Csv(Row("ERTS-2026-000001"), Row("ERTS-2026-000001")));

        Assert.Equal(1, run.InsertedCount);
        Assert.Equal(1, run.RejectedCount);
        Assert.Contains("Duplicate ReportNumber", db.Context.QuarantinedRecords.Single().Reasons,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_missing_required_columns_fails_the_run()
    {
        using var db = new TestDatabase();

        var run = await Import(db, "Foo,Bar\n1,2");

        Assert.Equal(ImportRunStatus.Failed, run.Status);
        Assert.Contains("Missing required column", run.FailureReason, StringComparison.Ordinal);
        Assert.Empty(db.Context.Incidents);
    }

    [Fact]
    public async Task Quoted_fields_with_commas_are_parsed_correctly()
    {
        using var db = new TestDatabase();
        var row = "ERTS-2026-000009,2026-06-01T14:00:00Z,,\"Transfer drip, contained on apron.\"," +
                  "47.0605,-122.9007,\"March Point, Anacortes\",Fidalgo Bay,Skagit,Marine Water," +
                  "\"Bunker C (No. 6 fuel oil)\",40,,Facility,\"Puget Terminal Operations, LLC\",Reported";

        var run = await Import(db, Csv(row));

        Assert.Equal(1, run.InsertedCount);
        var incident = db.Context.Incidents.Single();
        Assert.Equal("Transfer drip, contained on apron.", incident.Description);
        Assert.Equal("March Point, Anacortes", incident.LocationDescription);
        Assert.Equal("Puget Terminal Operations, LLC", incident.ResponsibleParty);
    }

    [Fact]
    public async Task Sample_dataset_imports_cleanly()
    {
        using var db = new TestDatabase();
        var path = FindRepoFile(Path.Combine("data", "sample", "spill_incidents_2020_2026.csv"));

        using var reader = new StreamReader(path);
        var run = await Service(db).ImportAsync("sample", reader);

        Assert.Equal(ImportRunStatus.Succeeded, run.Status);
        Assert.Equal(700, run.InsertedCount);
        Assert.Equal(0, run.RejectedCount);
    }

    [Fact]
    public async Task Quarantine_demo_dataset_rejects_exactly_the_bad_rows()
    {
        using var db = new TestDatabase();
        var path = FindRepoFile(Path.Combine("data", "sample", "quarantine_demo.csv"));

        using var reader = new StreamReader(path);
        var run = await Service(db).ImportAsync("quarantine_demo", reader);

        Assert.Equal(ImportRunStatus.CompletedWithRejects, run.Status);
        Assert.Equal(10, run.TotalRows);
        Assert.Equal(3, run.InsertedCount);
        Assert.Equal(7, run.RejectedCount);
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
