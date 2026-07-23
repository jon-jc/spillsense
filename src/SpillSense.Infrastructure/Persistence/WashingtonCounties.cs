using SpillSense.Domain.Geography;

namespace SpillSense.Infrastructure.Persistence;

/// <summary>
/// Seed data: all 39 Washington counties with FIPS codes, the Department of
/// Ecology regional office that covers them, and whether they border marine water.
/// </summary>
public static class WashingtonCounties
{
    public static IReadOnlyList<County> All { get; } =
    [
        New(1, "Adams", "53001", EcologyRegion.Eastern, false),
        New(2, "Asotin", "53003", EcologyRegion.Eastern, false),
        New(3, "Benton", "53005", EcologyRegion.Central, false),
        New(4, "Chelan", "53007", EcologyRegion.Central, false),
        New(5, "Clallam", "53009", EcologyRegion.Southwest, true),
        New(6, "Clark", "53011", EcologyRegion.Southwest, false),
        New(7, "Columbia", "53013", EcologyRegion.Eastern, false),
        New(8, "Cowlitz", "53015", EcologyRegion.Southwest, false),
        New(9, "Douglas", "53017", EcologyRegion.Central, false),
        New(10, "Ferry", "53019", EcologyRegion.Eastern, false),
        New(11, "Franklin", "53021", EcologyRegion.Eastern, false),
        New(12, "Garfield", "53023", EcologyRegion.Eastern, false),
        New(13, "Grant", "53025", EcologyRegion.Eastern, false),
        New(14, "Grays Harbor", "53027", EcologyRegion.Southwest, true),
        New(15, "Island", "53029", EcologyRegion.Northwest, true),
        New(16, "Jefferson", "53031", EcologyRegion.Southwest, true),
        New(17, "King", "53033", EcologyRegion.Northwest, true),
        New(18, "Kitsap", "53035", EcologyRegion.Northwest, true),
        New(19, "Kittitas", "53037", EcologyRegion.Central, false),
        New(20, "Klickitat", "53039", EcologyRegion.Central, false),
        New(21, "Lewis", "53041", EcologyRegion.Southwest, false),
        New(22, "Lincoln", "53043", EcologyRegion.Eastern, false),
        New(23, "Mason", "53045", EcologyRegion.Southwest, true),
        New(24, "Okanogan", "53047", EcologyRegion.Central, false),
        New(25, "Pacific", "53049", EcologyRegion.Southwest, true),
        New(26, "Pend Oreille", "53051", EcologyRegion.Eastern, false),
        New(27, "Pierce", "53053", EcologyRegion.Southwest, true),
        New(28, "San Juan", "53055", EcologyRegion.Northwest, true),
        New(29, "Skagit", "53057", EcologyRegion.Northwest, true),
        New(30, "Skamania", "53059", EcologyRegion.Southwest, false),
        New(31, "Snohomish", "53061", EcologyRegion.Northwest, true),
        New(32, "Spokane", "53063", EcologyRegion.Eastern, false),
        New(33, "Stevens", "53065", EcologyRegion.Eastern, false),
        New(34, "Thurston", "53067", EcologyRegion.Southwest, true),
        New(35, "Wahkiakum", "53069", EcologyRegion.Southwest, false),
        New(36, "Walla Walla", "53071", EcologyRegion.Eastern, false),
        New(37, "Whatcom", "53073", EcologyRegion.Northwest, true),
        New(38, "Whitman", "53075", EcologyRegion.Eastern, false),
        New(39, "Yakima", "53077", EcologyRegion.Central, false),
    ];

    private static County New(int id, string name, string fips, EcologyRegion region, bool coastal) =>
        new() { Id = id, Name = name, FipsCode = fips, Region = region, IsCoastal = coastal };
}
