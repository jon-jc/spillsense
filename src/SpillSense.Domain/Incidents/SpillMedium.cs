namespace SpillSense.Domain.Incidents;

/// <summary>
/// The environmental medium primarily affected by a spill.
/// </summary>
public enum SpillMedium
{
    Unknown = 0,
    MarineWater = 1,
    FreshWater = 2,
    Groundwater = 3,
    Land = 4,
    Air = 5,
}
