using SpillSense.Domain.Incidents;
using SpillSense.Infrastructure.Etl;

namespace SpillSense.Tests.Etl;

public class IncidentRowValidatorTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IncidentRowValidator Validator = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thurston"] = 34,
            ["King"] = 17,
        });

    private static IncidentCsvRow ValidRow(Action<Dictionary<string, string?>>? mutate = null)
    {
        var fields = new Dictionary<string, string?>
        {
            ["ReportNumber"] = "ERTS-2026-000123",
            ["ReportedAt"] = "2026-06-01T14:00:00Z",
            ["OccurredAt"] = "2026-06-01T09:30:00Z",
            ["Description"] = "Sheen near fuel dock.",
            ["Latitude"] = "47.0605",
            ["Longitude"] = "-122.9007",
            ["County"] = "Thurston",
            ["Medium"] = "Marine Water",
            ["SubstanceName"] = "Diesel fuel",
            ["QuantityGallons"] = "12.5",
            ["RecoveredGallons"] = "6",
            ["SourceType"] = "Vessel",
            ["Status"] = "Cleanup In Progress",
        };
        mutate?.Invoke(fields);

        return new IncidentCsvRow
        {
            RowNumber = 1,
            RawRow = "raw",
            ReportNumber = fields["ReportNumber"],
            ReportedAt = fields["ReportedAt"],
            OccurredAt = fields["OccurredAt"],
            Description = fields["Description"],
            Latitude = fields["Latitude"],
            Longitude = fields["Longitude"],
            County = fields["County"],
            Medium = fields["Medium"],
            SubstanceName = fields["SubstanceName"],
            QuantityGallons = fields["QuantityGallons"],
            RecoveredGallons = fields["RecoveredGallons"],
            SourceType = fields["SourceType"],
            Status = fields["Status"],
        };
    }

    [Fact]
    public void Valid_row_maps_to_incident()
    {
        var result = Validator.Validate(ValidRow(), Now);

        Assert.True(result.IsValid);
        var incident = result.Incident!;
        Assert.Equal("ERTS-2026-000123", incident.ReportNumber);
        Assert.Equal(SpillMedium.MarineWater, incident.Medium);
        Assert.Equal(SubstanceCategory.DieselFuel, incident.SubstanceCategory);
        Assert.Equal(IncidentStatus.CleanupInProgress, incident.Status);
        Assert.Equal(34, incident.CountyId);
        Assert.Equal(12.5m, incident.QuantityGallons);
    }

    [Theory]
    [InlineData("ReportNumber", null, "ReportNumber is required")]
    [InlineData("ReportNumber", "ERTS-26-123", "does not match")]
    [InlineData("ReportedAt", "not-a-date", "not a recognizable date")]
    [InlineData("ReportedAt", "2027-01-01T00:00:00Z", "in the future")]
    [InlineData("SubstanceName", null, "SubstanceName is required")]
    [InlineData("County", "Jefferson Davis", "not a Washington county")]
    [InlineData("Medium", "Salt Water", "not a recognized value")]
    [InlineData("QuantityGallons", "-5", "cannot be negative")]
    [InlineData("QuantityGallons", "lots", "not numeric")]
    public void Invalid_field_quarantines_row(string field, string? value, string expectedError)
    {
        var result = Validator.Validate(ValidRow(f => f[field] = value), Now);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public void Swapped_coordinates_are_rejected_with_guidance()
    {
        var result = Validator.Validate(
            ValidRow(f => (f["Latitude"], f["Longitude"]) = ("-122.9007", "47.0605")), Now);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("outside Washington State bounds", StringComparison.Ordinal));
    }

    [Fact]
    public void Latitude_without_longitude_is_rejected()
    {
        var result = Validator.Validate(ValidRow(f => f["Longitude"] = null), Now);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must be provided together", StringComparison.Ordinal));
    }

    [Fact]
    public void Occurred_after_reported_is_rejected()
    {
        var result = Validator.Validate(ValidRow(f => f["OccurredAt"] = "2026-06-02T00:00:00Z"), Now);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("OccurredAt is after ReportedAt", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_problem_on_a_row_is_reported_at_once()
    {
        var result = Validator.Validate(
            ValidRow(f =>
            {
                f["ReportNumber"] = "bogus";
                f["SubstanceName"] = null;
                f["QuantityGallons"] = "-1";
            }), Now);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void County_suffix_and_case_are_tolerated()
    {
        var result = Validator.Validate(ValidRow(f => f["County"] = "king county"), Now);

        Assert.True(result.IsValid);
        Assert.Equal(17, result.Incident!.CountyId);
    }

    [Fact]
    public void Missing_optional_classifications_default_to_unknown()
    {
        var result = Validator.Validate(
            ValidRow(f =>
            {
                f["Medium"] = null;
                f["SourceType"] = null;
                f["Status"] = null;
                f["OccurredAt"] = null;
            }), Now);

        Assert.True(result.IsValid);
        Assert.Equal(SpillMedium.Unknown, result.Incident!.Medium);
        Assert.Equal(SourceType.Unknown, result.Incident.SourceType);
        Assert.Equal(IncidentStatus.Reported, result.Incident.Status);
    }
}
