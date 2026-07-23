using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace SpillSense.Infrastructure.Etl;

/// <summary>
/// Streams incident rows out of a CSV file. Field values are kept as raw
/// strings; only structural problems (missing required headers) fail the file.
/// </summary>
public static class IncidentCsvReader
{
    private static readonly string[] RequiredHeaders =
        ["ReportNumber", "ReportedAt", "Description", "SubstanceName"];

    public static IEnumerable<IncidentCsvRow> Read(TextReader reader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new InvalidDataException("File is empty or has no header row.");
        }

        var header = csv.HeaderRecord ?? [];
        var missing = RequiredHeaders
            .Where(h => !header.Contains(h, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"Missing required column(s): {string.Join(", ", missing)}.");
        }

        var rowNumber = 0;
        while (csv.Read())
        {
            rowNumber++;
            yield return new IncidentCsvRow
            {
                RowNumber = rowNumber,
                RawRow = csv.Parser.RawRecord.TrimEnd('\r', '\n'),
                ReportNumber = Field(csv, "ReportNumber"),
                ReportedAt = Field(csv, "ReportedAt"),
                OccurredAt = Field(csv, "OccurredAt"),
                Description = Field(csv, "Description"),
                Latitude = Field(csv, "Latitude"),
                Longitude = Field(csv, "Longitude"),
                LocationDescription = Field(csv, "LocationDescription"),
                Waterbody = Field(csv, "Waterbody"),
                County = Field(csv, "County"),
                Medium = Field(csv, "Medium"),
                SubstanceName = Field(csv, "SubstanceName"),
                QuantityGallons = Field(csv, "QuantityGallons"),
                RecoveredGallons = Field(csv, "RecoveredGallons"),
                SourceType = Field(csv, "SourceType"),
                ResponsibleParty = Field(csv, "ResponsibleParty"),
                Status = Field(csv, "Status"),
            };
        }
    }

    private static string? Field(CsvReader csv, string name)
    {
        var value = csv.TryGetField<string>(name, out var field) ? field : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
