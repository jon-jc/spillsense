namespace SpillSense.Domain.Incidents;

/// <summary>
/// The kind of source the spill originated from.
/// </summary>
public enum SourceType
{
    Unknown = 0,
    Vessel = 1,
    Facility = 2,
    Pipeline = 3,
    Vehicle = 4,
    RailCar = 5,
    Aircraft = 6,
    Other = 7,
}
