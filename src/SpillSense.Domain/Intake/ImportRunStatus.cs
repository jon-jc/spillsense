namespace SpillSense.Domain.Intake;

public enum ImportRunStatus
{
    Running = 0,

    /// <summary>Every row imported cleanly.</summary>
    Succeeded = 1,

    /// <summary>Run completed but some rows were quarantined.</summary>
    CompletedWithRejects = 2,

    /// <summary>Run aborted; no partial results were kept.</summary>
    Failed = 3,
}
