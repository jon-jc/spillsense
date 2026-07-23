using SpillSense.Domain.Incidents;

namespace SpillSense.Tests.Domain;

public class SubstanceClassifierTests
{
    [Theory]
    [InlineData("Diesel fuel", SubstanceCategory.DieselFuel)]
    [InlineData("red-dyed DIESEL", SubstanceCategory.DieselFuel)]
    [InlineData("Alaska North Slope crude", SubstanceCategory.CrudeOil)]
    [InlineData("Bunker C (No. 6 fuel oil)", SubstanceCategory.HeavyFuelOil)]
    [InlineData("Jet fuel (Jet-A)", SubstanceCategory.JetFuel)]
    [InlineData("Hydraulic oil", SubstanceCategory.LubeOrHydraulicOil)]
    [InlineData("Oily bilge water", SubstanceCategory.BilgeOrOilyWater)]
    [InlineData("Antifreeze (ethylene glycol)", SubstanceCategory.Chemical)]
    [InlineData("Untreated wastewater", SubstanceCategory.Sewage)]
    [InlineData("Gasoline", SubstanceCategory.Gasoline)]
    [InlineData("Mystery sheen", SubstanceCategory.Other)]
    [InlineData("", SubstanceCategory.Unknown)]
    [InlineData(null, SubstanceCategory.Unknown)]
    public void Classifies_field_report_names(string? name, SubstanceCategory expected) =>
        Assert.Equal(expected, SubstanceClassifier.Classify(name));
}
