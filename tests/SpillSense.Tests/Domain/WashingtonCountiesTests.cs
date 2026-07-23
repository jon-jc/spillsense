using SpillSense.Domain.Geography;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Tests.Domain;

public class WashingtonCountiesTests
{
    [Fact]
    public void Contains_all_39_washington_counties() =>
        Assert.Equal(39, WashingtonCounties.All.Count);

    [Fact]
    public void County_names_are_unique() =>
        Assert.Equal(39, WashingtonCounties.All.Select(c => c.Name).Distinct().Count());

    [Fact]
    public void Fips_codes_are_unique_and_well_formed()
    {
        Assert.All(WashingtonCounties.All, c =>
        {
            Assert.Matches(@"^53\d{3}$", c.FipsCode);
        });
        Assert.Equal(39, WashingtonCounties.All.Select(c => c.FipsCode).Distinct().Count());
    }

    [Fact]
    public void Every_county_is_assigned_an_ecology_region() =>
        Assert.DoesNotContain(WashingtonCounties.All, c => c.Region == EcologyRegion.Unknown);

    [Theory]
    [InlineData("Thurston", EcologyRegion.Southwest, true)]
    [InlineData("King", EcologyRegion.Northwest, true)]
    [InlineData("Spokane", EcologyRegion.Eastern, false)]
    [InlineData("Yakima", EcologyRegion.Central, false)]
    public void Spot_checks_region_and_coastal_flags(string name, EcologyRegion region, bool coastal)
    {
        var county = Assert.Single(WashingtonCounties.All, c => c.Name == name);
        Assert.Equal(region, county.Region);
        Assert.Equal(coastal, county.IsCoastal);
    }
}
