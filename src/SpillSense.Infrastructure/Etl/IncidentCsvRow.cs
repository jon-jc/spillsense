namespace SpillSense.Infrastructure.Etl;

/// <summary>
/// One raw row from an incident CSV, untouched strings only.
/// Parsing and validation happen in <see cref="IncidentRowValidator"/> so a
/// malformed value can be reported instead of throwing mid-file.
/// </summary>
public sealed record IncidentCsvRow
{
    /// <summary>1-based data row number (header excluded).</summary>
    public required int RowNumber { get; init; }

    /// <summary>The row exactly as it appeared in the file.</summary>
    public required string RawRow { get; init; }

    public string? ReportNumber { get; init; }
    public string? ReportedAt { get; init; }
    public string? OccurredAt { get; init; }
    public string? Description { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
    public string? LocationDescription { get; init; }
    public string? Waterbody { get; init; }
    public string? County { get; init; }
    public string? Medium { get; init; }
    public string? SubstanceName { get; init; }
    public string? QuantityGallons { get; init; }
    public string? RecoveredGallons { get; init; }
    public string? SourceType { get; init; }
    public string? ResponsibleParty { get; init; }
    public string? Status { get; init; }
}
