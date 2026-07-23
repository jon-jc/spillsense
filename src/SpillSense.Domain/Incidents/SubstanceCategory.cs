namespace SpillSense.Domain.Incidents;

/// <summary>
/// Broad classification of the spilled substance, used for reporting rollups.
/// The free-text substance name is preserved on the incident itself.
/// </summary>
public enum SubstanceCategory
{
    Unknown = 0,
    CrudeOil = 1,
    DieselFuel = 2,
    Gasoline = 3,
    JetFuel = 4,
    HeavyFuelOil = 5,
    LubeOrHydraulicOil = 6,
    BilgeOrOilyWater = 7,
    Chemical = 8,
    Sewage = 9,
    Other = 10,
}
