namespace SpillSense.Domain.Incidents;

/// <summary>
/// Lifecycle state of an incident record.
/// </summary>
public enum IncidentStatus
{
    Reported = 0,
    UnderInvestigation = 1,
    CleanupInProgress = 2,
    Closed = 3,
    ReferredToOtherAgency = 4,
}
