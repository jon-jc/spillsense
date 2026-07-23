using SpillSense.Domain.Incidents;

namespace SpillSense.Infrastructure.Etl;

/// <summary>
/// Outcome of validating and mapping one CSV row: either a fully mapped
/// incident, or the list of reasons the row must be quarantined.
/// </summary>
public sealed class RowValidationResult
{
    private RowValidationResult(SpillIncident? incident, IReadOnlyList<string> errors)
    {
        Incident = incident;
        Errors = errors;
    }

    public SpillIncident? Incident { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Incident is not null;

    public static RowValidationResult Valid(SpillIncident incident) => new(incident, []);
    public static RowValidationResult Invalid(IReadOnlyList<string> errors) => new(null, errors);
}
