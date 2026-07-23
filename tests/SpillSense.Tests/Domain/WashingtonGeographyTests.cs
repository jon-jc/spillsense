using SpillSense.Domain.Geography;

namespace SpillSense.Tests.Domain;

public class WashingtonGeographyTests
{
    [Theory]
    [InlineData(47.0379, -122.9007)] // Olympia
    [InlineData(48.5126, -122.6127)] // Anacortes
    [InlineData(46.2087, -119.1199)] // Kennewick
    [InlineData(48.3733, -124.7250)] // Strait of Juan de Fuca, offshore
    public void Accepts_coordinates_inside_washington(double lat, double lon) =>
        Assert.True(WashingtonGeography.IsWithinState(lat, lon));

    [Theory]
    [InlineData(-122.9007, 47.0379)] // swapped lat/lon
    [InlineData(47.0379, 122.9007)]  // missing negative sign on longitude
    [InlineData(0, 0)]               // null island
    [InlineData(34.05, -118.24)]     // Los Angeles
    public void Rejects_coordinates_outside_washington(double lat, double lon) =>
        Assert.False(WashingtonGeography.IsWithinState(lat, lon));
}
