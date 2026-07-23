namespace SpillSense.Domain.Intake;

/// <summary>
/// A source row that failed intake validation. The raw row is preserved
/// verbatim so data stewards can inspect, correct, and re-submit it;
/// bad data is never silently dropped.
/// </summary>
public class QuarantinedRecord
{
    public int Id { get; set; }

    public int ImportRunId { get; set; }
    public ImportRun? ImportRun { get; set; }

    /// <summary>1-based data row number in the source file (excluding the header).</summary>
    public int RowNumber { get; set; }

    /// <summary>The report number from the row, when one could be read.</summary>
    public string? ReportNumber { get; set; }

    /// <summary>The raw source row, verbatim.</summary>
    public required string RawRow { get; set; }

    /// <summary>Human-readable validation failures, one per line.</summary>
    public required string Reasons { get; set; }
}
