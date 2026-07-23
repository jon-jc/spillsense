using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpillSense.Domain.Intake;
using SpillSense.Domain.Incidents;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Infrastructure.Etl;

/// <summary>
/// Intake pipeline for incident CSV files.
///
/// Guarantees:
///  - Idempotent: re-running the same file inserts nothing new; changed rows
///    update the existing incident (matched on ReportNumber).
///  - Nothing dropped silently: invalid rows land in quarantine with the raw
///    row and every validation failure, tied to an auditable import run.
///  - Streaming: rows are processed in batches, so file size is not bounded
///    by memory.
/// </summary>
public partial class IncidentImportService
{
    private const int BatchSize = 500;

    private readonly SpillSenseDbContext _db;
    private readonly ILogger<IncidentImportService> _logger;

    public IncidentImportService(SpillSenseDbContext db, ILogger<IncidentImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ImportRun> ImportAsync(
        string sourceName, TextReader reader, CancellationToken cancellationToken = default)
    {
        var run = new ImportRun
        {
            SourceName = sourceName,
            StartedAtUtc = DateTime.UtcNow,
            Status = ImportRunStatus.Running,
        };
        _db.ImportRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var countyIds = await _db.Counties
                .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var validator = new IncidentRowValidator(countyIds);
            var seenReportNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var batch in IncidentCsvReader.Read(reader).Chunk(BatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessBatchAsync(run, batch, validator, seenReportNumbers, cancellationToken);
            }

            run.Status = run.RejectedCount == 0
                ? ImportRunStatus.Succeeded
                : ImportRunStatus.CompletedWithRejects;
        }
        catch (InvalidDataException ex)
        {
            run.Status = ImportRunStatus.Failed;
            run.FailureReason = ex.Message;
            LogImportFailed(ex, sourceName);
        }

        run.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        LogImportCompleted(
            sourceName, run.TotalRows, run.InsertedCount, run.UpdatedCount,
            run.UnchangedCount, run.RejectedCount);

        return run;
    }

    private async Task ProcessBatchAsync(
        ImportRun run,
        IncidentCsvRow[] batch,
        IncidentRowValidator validator,
        HashSet<string> seenReportNumbers,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var validRows = new List<SpillIncident>();

        foreach (var row in batch)
        {
            run.TotalRows++;

            var result = validator.Validate(row, utcNow);
            if (!result.IsValid)
            {
                Quarantine(run, row, result.Errors);
                continue;
            }

            var incident = result.Incident!;
            if (!seenReportNumbers.Add(incident.ReportNumber))
            {
                Quarantine(run, row, [$"Duplicate ReportNumber '{incident.ReportNumber}' earlier in this file."]);
                continue;
            }

            validRows.Add(incident);
        }

        var incoming = validRows.Select(i => i.ReportNumber).ToList();
        var existing = await _db.Incidents
            .Where(i => incoming.Contains(i.ReportNumber))
            .ToDictionaryAsync(i => i.ReportNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var incident in validRows)
        {
            if (existing.TryGetValue(incident.ReportNumber, out var current))
            {
                if (ApplyChanges(current, incident))
                {
                    run.UpdatedCount++;
                }
                else
                {
                    run.UnchangedCount++;
                }
            }
            else
            {
                _db.Incidents.Add(incident);
                run.InsertedCount++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void Quarantine(ImportRun run, IncidentCsvRow row, IReadOnlyList<string> reasons)
    {
        run.RejectedCount++;
        _db.QuarantinedRecords.Add(new QuarantinedRecord
        {
            ImportRunId = run.Id,
            RowNumber = row.RowNumber,
            ReportNumber = row.ReportNumber,
            RawRow = row.RawRow,
            Reasons = string.Join('\n', reasons),
        });
    }

    /// <summary>Copies incoming values onto the tracked entity; true if anything changed.</summary>
    private static bool ApplyChanges(SpillIncident current, SpillIncident incoming)
    {
        var changed = false;

        changed |= Set(current.ReportedAtUtc, incoming.ReportedAtUtc, v => current.ReportedAtUtc = v);
        changed |= Set(current.OccurredAtUtc, incoming.OccurredAtUtc, v => current.OccurredAtUtc = v);
        changed |= Set(current.Description, incoming.Description, v => current.Description = v);
        changed |= Set(current.Latitude, incoming.Latitude, v => current.Latitude = v);
        changed |= Set(current.Longitude, incoming.Longitude, v => current.Longitude = v);
        changed |= Set(current.LocationDescription, incoming.LocationDescription, v => current.LocationDescription = v);
        changed |= Set(current.WaterbodyName, incoming.WaterbodyName, v => current.WaterbodyName = v);
        changed |= Set(current.CountyId, incoming.CountyId, v => current.CountyId = v);
        changed |= Set(current.Medium, incoming.Medium, v => current.Medium = v);
        changed |= Set(current.SubstanceName, incoming.SubstanceName, v => current.SubstanceName = v);
        changed |= Set(current.SubstanceCategory, incoming.SubstanceCategory, v => current.SubstanceCategory = v);
        changed |= Set(current.QuantityGallons, incoming.QuantityGallons, v => current.QuantityGallons = v);
        changed |= Set(current.RecoveredGallons, incoming.RecoveredGallons, v => current.RecoveredGallons = v);
        changed |= Set(current.SourceType, incoming.SourceType, v => current.SourceType = v);
        changed |= Set(current.ResponsibleParty, incoming.ResponsibleParty, v => current.ResponsibleParty = v);
        changed |= Set(current.Status, incoming.Status, v => current.Status = v);

        return changed;
    }

    private static bool Set<T>(T currentValue, T incomingValue, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, incomingValue))
        {
            return false;
        }

        assign(incomingValue);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Import of {SourceName} failed")]
    private partial void LogImportFailed(Exception exception, string sourceName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Import {SourceName}: {Total} rows - {Inserted} inserted, {Updated} updated, {Unchanged} unchanged, {Rejected} quarantined")]
    private partial void LogImportCompleted(
        string sourceName, int total, int inserted, int updated, int unchanged, int rejected);
}
