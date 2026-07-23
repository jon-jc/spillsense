namespace SpillSense.Domain.Intake;

/// <summary>
/// One execution of the intake pipeline against a source file.
/// Row counts make every import auditable after the fact.
/// </summary>
public class ImportRun
{
    public int Id { get; set; }

    public required string SourceName { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ImportRunStatus Status { get; set; }

    public int TotalRows { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int RejectedCount { get; set; }

    /// <summary>Set when the run aborts before completing (unreadable file, etc.).</summary>
    public string? FailureReason { get; set; }

    public ICollection<QuarantinedRecord> QuarantinedRecords { get; set; } = [];
}
