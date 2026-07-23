using SpillSense.Domain.Incidents;
using SpillSense.Domain.Intake;

namespace SpillSense.Web.Api;

public sealed record IncidentSummaryDto(
    int Id,
    string ReportNumber,
    DateTime ReportedAtUtc,
    string? County,
    string Medium,
    string SubstanceName,
    string SubstanceCategory,
    decimal? QuantityGallons,
    string SourceType,
    string Status,
    double? Latitude,
    double? Longitude);

public sealed record IncidentDetailDto(
    int Id,
    string ReportNumber,
    DateTime ReportedAtUtc,
    DateTime? OccurredAtUtc,
    string Description,
    double? Latitude,
    double? Longitude,
    string? LocationDescription,
    string? WaterbodyName,
    string? County,
    string? EcologyRegion,
    string Medium,
    string SubstanceName,
    string SubstanceCategory,
    decimal? QuantityGallons,
    decimal? RecoveredGallons,
    string SourceType,
    string? ResponsibleParty,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PagedResult<T>(int Total, int Page, int PageSize, IReadOnlyList<T> Items);

public sealed record ImportRunDto(
    int Id,
    string SourceName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    int TotalRows,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    int RejectedCount,
    string? FailureReason);

public sealed record QuarantinedRecordDto(
    int Id,
    int RowNumber,
    string? ReportNumber,
    string RawRow,
    IReadOnlyList<string> Reasons);

public static class DtoMappings
{
    public static IncidentSummaryDto ToSummaryDto(this SpillIncident i) => new(
        i.Id, i.ReportNumber, i.ReportedAtUtc, i.County?.Name,
        i.Medium.ToString(), i.SubstanceName, i.SubstanceCategory.ToString(),
        i.QuantityGallons, i.SourceType.ToString(), i.Status.ToString(),
        i.Latitude, i.Longitude);

    public static IncidentDetailDto ToDetailDto(this SpillIncident i) => new(
        i.Id, i.ReportNumber, i.ReportedAtUtc, i.OccurredAtUtc, i.Description,
        i.Latitude, i.Longitude, i.LocationDescription, i.WaterbodyName,
        i.County?.Name, i.County?.Region.ToString(),
        i.Medium.ToString(), i.SubstanceName, i.SubstanceCategory.ToString(),
        i.QuantityGallons, i.RecoveredGallons, i.SourceType.ToString(),
        i.ResponsibleParty, i.Status.ToString(), i.CreatedAtUtc, i.UpdatedAtUtc);

    public static ImportRunDto ToDto(this ImportRun r) => new(
        r.Id, r.SourceName, r.StartedAtUtc, r.CompletedAtUtc, r.Status.ToString(),
        r.TotalRows, r.InsertedCount, r.UpdatedCount, r.UnchangedCount,
        r.RejectedCount, r.FailureReason);

    public static QuarantinedRecordDto ToDto(this QuarantinedRecord q) => new(
        q.Id, q.RowNumber, q.ReportNumber, q.RawRow, q.Reasons.Split('\n'));
}
