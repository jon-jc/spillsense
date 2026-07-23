using System.Globalization;
using System.Text.RegularExpressions;
using SpillSense.Domain.Geography;
using SpillSense.Domain.Incidents;

namespace SpillSense.Infrastructure.Etl;

/// <summary>
/// Field-level validation and mapping for incident CSV rows. Collects every
/// problem on a row (not just the first) so a data steward can fix the row in
/// one pass.
/// </summary>
public sealed partial class IncidentRowValidator
{
    [GeneratedRegex(@"^ERTS-\d{4}-\d{6}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReportNumberFormat();

    private readonly IReadOnlyDictionary<string, int> _countyIdsByName;

    /// <param name="countyIdsByName">County name (without "County" suffix) to id, case-insensitive.</param>
    public IncidentRowValidator(IReadOnlyDictionary<string, int> countyIdsByName)
    {
        _countyIdsByName = countyIdsByName;
    }

    public RowValidationResult Validate(IncidentCsvRow row, DateTime utcNow)
    {
        var errors = new List<string>();

        // Identity and required narrative fields.
        if (row.ReportNumber is null)
        {
            errors.Add("ReportNumber is required.");
        }
        else if (!ReportNumberFormat().IsMatch(row.ReportNumber))
        {
            errors.Add($"ReportNumber '{row.ReportNumber}' does not match ERTS-YYYY-NNNNNN.");
        }

        if (row.Description is null)
        {
            errors.Add("Description is required.");
        }

        if (row.SubstanceName is null)
        {
            errors.Add("SubstanceName is required.");
        }

        // Dates.
        DateTime? reportedAt = null;
        if (row.ReportedAt is null)
        {
            errors.Add("ReportedAt is required.");
        }
        else if (!TryParseUtc(row.ReportedAt, out var parsed))
        {
            errors.Add($"ReportedAt '{row.ReportedAt}' is not a recognizable date/time.");
        }
        else if (parsed > utcNow.AddHours(1))
        {
            errors.Add($"ReportedAt '{row.ReportedAt}' is in the future.");
        }
        else
        {
            reportedAt = parsed;
        }

        DateTime? occurredAt = null;
        if (row.OccurredAt is not null)
        {
            if (!TryParseUtc(row.OccurredAt, out var parsed))
            {
                errors.Add($"OccurredAt '{row.OccurredAt}' is not a recognizable date/time.");
            }
            else if (reportedAt.HasValue && parsed > reportedAt.Value)
            {
                errors.Add("OccurredAt is after ReportedAt.");
            }
            else
            {
                occurredAt = parsed;
            }
        }

        // Coordinates: both-or-neither, and inside Washington when present.
        double? latitude = null, longitude = null;
        if (row.Latitude is not null || row.Longitude is not null)
        {
            if (row.Latitude is null || row.Longitude is null)
            {
                errors.Add("Latitude and Longitude must be provided together.");
            }
            else if (!TryParseDouble(row.Latitude, out var lat) || !TryParseDouble(row.Longitude, out var lon))
            {
                errors.Add($"Coordinates '{row.Latitude}, {row.Longitude}' are not numeric.");
            }
            else if (!WashingtonGeography.IsWithinState(lat, lon))
            {
                errors.Add($"Coordinates ({lat}, {lon}) fall outside Washington State bounds " +
                           "(check for swapped values or a missing negative sign on longitude).");
            }
            else
            {
                latitude = lat;
                longitude = lon;
            }
        }

        // County reference lookup.
        int? countyId = null;
        if (row.County is not null)
        {
            var name = NormalizeCountyName(row.County);
            if (_countyIdsByName.TryGetValue(name, out var id))
            {
                countyId = id;
            }
            else
            {
                errors.Add($"County '{row.County}' is not a Washington county.");
            }
        }

        // Quantities.
        decimal? quantity = ParseQuantity(row.QuantityGallons, "QuantityGallons", errors);
        decimal? recovered = ParseQuantity(row.RecoveredGallons, "RecoveredGallons", errors);

        // Classifications.
        var medium = ParseEnum<SpillMedium>(row.Medium, "Medium", errors);
        var sourceType = ParseEnum<SourceType>(row.SourceType, "SourceType", errors);
        var status = ParseEnum<IncidentStatus>(row.Status, "Status", errors) ?? IncidentStatus.Reported;

        if (errors.Count > 0)
        {
            return RowValidationResult.Invalid(errors);
        }

        return RowValidationResult.Valid(new SpillIncident
        {
            ReportNumber = row.ReportNumber!,
            ReportedAtUtc = reportedAt!.Value,
            OccurredAtUtc = occurredAt,
            Description = row.Description!,
            Latitude = latitude,
            Longitude = longitude,
            LocationDescription = row.LocationDescription,
            WaterbodyName = row.Waterbody,
            CountyId = countyId,
            Medium = medium ?? SpillMedium.Unknown,
            SubstanceName = row.SubstanceName!,
            SubstanceCategory = SubstanceClassifier.Classify(row.SubstanceName),
            QuantityGallons = quantity,
            RecoveredGallons = recovered,
            SourceType = sourceType ?? SourceType.Unknown,
            ResponsibleParty = row.ResponsibleParty,
            Status = status,
        });
    }

    private static bool TryParseUtc(string value, out DateTime result)
    {
        var ok = DateTime.TryParse(
            value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out result);
        return ok;
    }

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static decimal? ParseQuantity(string? value, string fieldName, List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            errors.Add($"{fieldName} '{value}' is not numeric.");
            return null;
        }

        if (parsed < 0)
        {
            errors.Add($"{fieldName} cannot be negative.");
            return null;
        }

        return parsed;
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string fieldName, List<string> errors)
        where TEnum : struct, Enum
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Replace(" ", "", StringComparison.Ordinal);
        if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{fieldName} '{value}' is not a recognized value " +
                   $"(expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}).");
        return null;
    }

    private static string NormalizeCountyName(string raw)
    {
        var name = raw.Trim();
        if (name.EndsWith(" County", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^7].TrimEnd();
        }

        return name;
    }
}
