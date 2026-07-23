using Microsoft.EntityFrameworkCore;
using SpillSense.Domain.Incidents;

namespace SpillSense.Tests.Persistence;

public class SpillSenseDbContextTests
{
    private static SpillIncident NewIncident(string reportNumber) => new()
    {
        ReportNumber = reportNumber,
        ReportedAtUtc = new DateTime(2025, 6, 1, 14, 30, 0, DateTimeKind.Utc),
        Description = "Sheen observed near marina fuel dock.",
        SubstanceName = "Diesel fuel",
        SubstanceCategory = SubstanceCategory.DieselFuel,
        Medium = SpillMedium.MarineWater,
        SourceType = SourceType.Vessel,
        Status = IncidentStatus.Reported,
        Latitude = 47.05,
        Longitude = -122.90,
    };

    [Fact]
    public void Migrations_seed_all_39_counties()
    {
        using var db = new TestDatabase();
        Assert.Equal(39, db.Context.Counties.Count());
    }

    [Fact]
    public void Report_number_is_enforced_unique_by_the_database()
    {
        using var db = new TestDatabase();

        db.Context.Incidents.Add(NewIncident("ERTS-2025-000001"));
        db.Context.SaveChanges();

        db.Context.Incidents.Add(NewIncident("ERTS-2025-000001"));
        Assert.Throws<DbUpdateException>(() => db.Context.SaveChanges());
    }

    [Fact]
    public void Audit_timestamps_are_stamped_on_insert_and_update()
    {
        using var db = new TestDatabase();

        var incident = NewIncident("ERTS-2025-000002");
        db.Context.Incidents.Add(incident);
        db.Context.SaveChanges();

        Assert.NotEqual(default, incident.CreatedAtUtc);
        Assert.Equal(incident.CreatedAtUtc, incident.UpdatedAtUtc);

        var created = incident.CreatedAtUtc;
        incident.Status = IncidentStatus.Closed;
        db.Context.SaveChanges();

        Assert.Equal(created, incident.CreatedAtUtc);
        Assert.True(incident.UpdatedAtUtc >= created);
    }

    [Fact]
    public void Incident_can_be_linked_to_a_seeded_county()
    {
        using var db = new TestDatabase();

        var thurston = db.Context.Counties.Single(c => c.Name == "Thurston");
        var incident = NewIncident("ERTS-2025-000003");
        incident.CountyId = thurston.Id;
        db.Context.Incidents.Add(incident);
        db.Context.SaveChanges();

        var loaded = db.Context.Incidents
            .Include(i => i.County)
            .Single(i => i.ReportNumber == "ERTS-2025-000003");
        Assert.Equal("Thurston", loaded.County!.Name);
    }

    [Fact]
    public void Enums_are_stored_as_readable_strings()
    {
        using var db = new TestDatabase();

        db.Context.Incidents.Add(NewIncident("ERTS-2025-000004"));
        db.Context.SaveChanges();

        var medium = db.Context.Database
            .SqlQueryRaw<string>("SELECT Medium AS Value FROM SpillIncidents WHERE ReportNumber = 'ERTS-2025-000004'")
            .Single();
        Assert.Equal("MarineWater", medium);
    }
}
