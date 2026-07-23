using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SpillSense.Domain.Incidents;
using SpillSense.Domain.Intake;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Tests.Web;

/// <summary>
/// Boots the real web host against a migrated temp-file SQLite database
/// seeded with a small, known set of incidents. Shared per test class.
/// </summary>
public sealed class ApiFixture : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"spillsense-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public ApiFixture()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            // Replace the DbContext registration outright: configuration-based
            // overrides are unreliable with minimal hosting, and a wrong path
            // would silently hit a shared database file.
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SpillSenseDbContext>>();
                services.AddDbContext<SpillSenseDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpillSenseDbContext>();
        Seed(db);
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    private static void Seed(SpillSenseDbContext db)
    {
        int CountyId(string name) => db.Counties.Single(c => c.Name == name).Id;

        db.Incidents.AddRange(
            new SpillIncident
            {
                ReportNumber = "ERTS-2025-000001",
                ReportedAtUtc = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                Description = "Fuel overflow during vessel bunkering at pier.",
                Latitude = 47.60, Longitude = -122.35,
                CountyId = CountyId("King"),
                WaterbodyName = "Elliott Bay",
                Medium = SpillMedium.MarineWater,
                SubstanceName = "Diesel fuel",
                SubstanceCategory = SubstanceCategory.DieselFuel,
                QuantityGallons = 100m, RecoveredGallons = 40m,
                SourceType = SourceType.Vessel,
                ResponsibleParty = "Cascadia Marine Services LLC",
                Status = IncidentStatus.Closed,
            },
            new SpillIncident
            {
                ReportNumber = "ERTS-2025-000002",
                ReportedAtUtc = new DateTime(2025, 6, 10, 14, 30, 0, DateTimeKind.Utc),
                Description = "Tank overfill at bulk facility reached stormwater drain.",
                Latitude = 47.05, Longitude = -122.90,
                CountyId = CountyId("Thurston"),
                WaterbodyName = "Budd Inlet",
                Medium = SpillMedium.MarineWater,
                SubstanceName = "Alaska North Slope crude",
                SubstanceCategory = SubstanceCategory.CrudeOil,
                QuantityGallons = 500m, RecoveredGallons = 350m,
                SourceType = SourceType.Facility,
                ResponsibleParty = "Puget Terminal Operations LLC",
                Status = IncidentStatus.UnderInvestigation,
            },
            new SpillIncident
            {
                ReportNumber = "ERTS-2024-000003",
                ReportedAtUtc = new DateTime(2024, 11, 2, 8, 15, 0, DateTimeKind.Utc),
                Description = "Saddle tank puncture after collision on I-90.",
                Latitude = 47.66, Longitude = -117.42,
                CountyId = CountyId("Spokane"),
                Medium = SpillMedium.FreshWater,
                SubstanceName = "Gasoline",
                SubstanceCategory = SubstanceCategory.Gasoline,
                QuantityGallons = 20m,
                SourceType = SourceType.Vehicle,
                Status = IncidentStatus.Closed,
            },
            new SpillIncident
            {
                ReportNumber = "ERTS-2026-000004",
                ReportedAtUtc = new DateTime(2026, 1, 20, 16, 45, 0, DateTimeKind.Utc),
                Description = "Mystery sheen reported by ferry crew; no source located.",
                Medium = SpillMedium.Unknown,
                SubstanceName = "Mystery sheen",
                SubstanceCategory = SubstanceCategory.Other,
                SourceType = SourceType.Unknown,
                Status = IncidentStatus.Reported,
            });

        var run = new ImportRun
        {
            SourceName = "seed.csv",
            StartedAtUtc = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 2, 1, 9, 0, 5, DateTimeKind.Utc),
            Status = ImportRunStatus.CompletedWithRejects,
            TotalRows = 5, InsertedCount = 4, RejectedCount = 1,
        };
        run.QuarantinedRecords.Add(new QuarantinedRecord
        {
            RowNumber = 5,
            ReportNumber = "ERTS-2026-999999",
            RawRow = "ERTS-2026-999999,bad-date,...",
            Reasons = "ReportedAt 'bad-date' is not a recognizable date/time.",
        });
        db.ImportRuns.Add(run);

        db.SaveChanges();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
