namespace SpillSense.Domain.Incidents;

/// <summary>
/// Maps free-text substance names from field reports to a reporting category.
/// Rules are ordered: the first match wins, so more specific terms come first.
/// </summary>
public static class SubstanceClassifier
{
    private static readonly (string[] Keywords, SubstanceCategory Category)[] Rules =
    [
        (["crude"], SubstanceCategory.CrudeOil),
        (["diesel", "marine gas oil", "mgo"], SubstanceCategory.DieselFuel),
        (["gasoline", "petrol", "unleaded"], SubstanceCategory.Gasoline),
        (["jet fuel", "jet-a", "jet a", "avgas", "aviation"], SubstanceCategory.JetFuel),
        (["bunker", "heavy fuel", "hfo", "no. 6", "residual fuel"], SubstanceCategory.HeavyFuelOil),
        (["hydraulic", "lube", "lubricat", "motor oil", "gear oil", "transmission"], SubstanceCategory.LubeOrHydraulicOil),
        (["bilge", "oily water", "oily bilge"], SubstanceCategory.BilgeOrOilyWater),
        (["sewage", "wastewater", "septic", "black water"], SubstanceCategory.Sewage),
        (["acid", "solvent", "ammonia", "chlorine", "sodium", "hydroxide", "formaldehyde",
          "antifreeze", "glycol", "paint", "pesticide", "herbicide", "fertilizer"], SubstanceCategory.Chemical),
    ];

    public static SubstanceCategory Classify(string? substanceName)
    {
        if (string.IsNullOrWhiteSpace(substanceName))
        {
            return SubstanceCategory.Unknown;
        }

        foreach (var (keywords, category) in Rules)
        {
            foreach (var keyword in keywords)
            {
                if (substanceName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
        }

        return SubstanceCategory.Other;
    }
}
