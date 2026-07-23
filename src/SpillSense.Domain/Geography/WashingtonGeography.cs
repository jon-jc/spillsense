namespace SpillSense.Domain.Geography;

/// <summary>
/// Coordinate sanity checks for Washington State, including adjacent marine waters.
/// Used by intake validation to reject obviously bad coordinates (swapped lat/lon,
/// missing negative sign on longitude, etc.) before they reach the database.
/// </summary>
public static class WashingtonGeography
{
    public const double MinLatitude = 45.30;
    public const double MaxLatitude = 49.10;
    public const double MinLongitude = -125.50;
    public const double MaxLongitude = -116.80;

    public static bool IsWithinState(double latitude, double longitude) =>
        latitude is >= MinLatitude and <= MaxLatitude &&
        longitude is >= MinLongitude and <= MaxLongitude;
}
