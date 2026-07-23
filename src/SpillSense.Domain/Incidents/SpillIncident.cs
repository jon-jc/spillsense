using SpillSense.Domain.Geography;

namespace SpillSense.Domain.Incidents;

/// <summary>
/// A reported oil or hazardous-material spill incident.
/// </summary>
public class SpillIncident
{
    public int Id { get; set; }

    /// <summary>
    /// Natural key from the source reporting system (Environmental Report Tracking
    /// System style), e.g. "ERTS-2024-012345". Unique; used for idempotent imports.
    /// </summary>
    public required string ReportNumber { get; set; }

    /// <summary>When the spill was reported to the program (UTC).</summary>
    public DateTime ReportedAtUtc { get; set; }

    /// <summary>When the spill actually occurred, if known (UTC).</summary>
    public DateTime? OccurredAtUtc { get; set; }

    public required string Description { get; set; }

    // Location. Coordinates are WGS 84 (EPSG:4326); nullable because some reports
    // arrive with only a narrative location.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? WaterbodyName { get; set; }

    public int? CountyId { get; set; }
    public County? County { get; set; }

    public SpillMedium Medium { get; set; }

    /// <summary>Free-text substance name as reported, e.g. "Diesel fuel, red-dyed".</summary>
    public required string SubstanceName { get; set; }

    public SubstanceCategory SubstanceCategory { get; set; }

    /// <summary>Estimated quantity spilled, in US gallons. Null when unquantified.</summary>
    public decimal? QuantityGallons { get; set; }

    /// <summary>Quantity recovered during response, in US gallons.</summary>
    public decimal? RecoveredGallons { get; set; }

    public SourceType SourceType { get; set; }

    /// <summary>Party identified as responsible, when known.</summary>
    public string? ResponsibleParty { get; set; }

    public IncidentStatus Status { get; set; }

    // Audit fields, set by the persistence layer.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>True when the incident carries usable map coordinates.</summary>
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
}
