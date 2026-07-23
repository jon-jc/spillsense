namespace SpillSense.Domain.Geography;

/// <summary>
/// A Washington State county. Reference data seeded at database creation.
/// </summary>
public class County
{
    public int Id { get; set; }

    /// <summary>County name without the "County" suffix, e.g. "Thurston".</summary>
    public required string Name { get; set; }

    /// <summary>Five-digit FIPS code, e.g. "53067".</summary>
    public required string FipsCode { get; set; }

    public EcologyRegion Region { get; set; }

    /// <summary>True if the county borders marine waters (Puget Sound, Strait, or Pacific coast).</summary>
    public bool IsCoastal { get; set; }
}
