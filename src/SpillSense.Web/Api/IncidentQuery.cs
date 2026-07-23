using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SpillSense.Domain.Geography;
using SpillSense.Domain.Incidents;

namespace SpillSense.Web.Api;

/// <summary>
/// Query-string parameters shared by the incident list, GeoJSON, and stats
/// endpoints. Raw strings are bound here and parsed by <see cref="Parse"/> so
/// bad input produces one helpful 400 instead of a framework binding error.
/// </summary>
public sealed class IncidentQuery
{
    public string? County { get; init; }
    public string? Region { get; init; }
    public string? Medium { get; init; }
    public string? Category { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }

    /// <summary>Inclusive lower bound on ReportedAt (date or ISO date-time, UTC).</summary>
    public string? From { get; init; }

    /// <summary>Exclusive upper bound on ReportedAt.</summary>
    public string? To { get; init; }

    /// <summary>Case-insensitive text search across substance, description, party, location, waterbody.</summary>
    public string? Search { get; init; }

    /// <summary>Spatial filter: "minLon,minLat,maxLon,maxLat" (WGS 84).</summary>
    public string? Bbox { get; init; }

    public decimal? MinGallons { get; init; }
    public bool? HasCoordinates { get; init; }

    // Nullable on purpose: [AsParameters] binding leaves absent value types at
    // default(T), so "not provided" must be distinguishable from an explicit 0.
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    /// <summary>reportedAt | quantity, prefix with '-' for descending. Default: -reportedAt.</summary>
    public string? Sort { get; init; }

    public ParsedIncidentQuery Parse()
    {
        var errors = new List<string>();

        var medium = ParseEnum<SpillMedium>(Medium, "medium", errors);
        var category = ParseEnum<SubstanceCategory>(Category, "category", errors);
        var source = ParseEnum<SourceType>(Source, "source", errors);
        var status = ParseEnum<IncidentStatus>(Status, "status", errors);
        var region = ParseEnum<EcologyRegion>(Region, "region", errors);

        var from = ParseDate(From, "from", errors);
        var to = ParseDate(To, "to", errors);
        if (from.HasValue && to.HasValue && from > to)
        {
            errors.Add("'from' must not be after 'to'.");
        }

        (double MinLon, double MinLat, double MaxLon, double MaxLat)? bbox = null;
        if (Bbox is not null)
        {
            var parts = Bbox.Split(',');
            if (parts.Length == 4
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLon)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLat)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLon)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLat)
                && minLon <= maxLon && minLat <= maxLat)
            {
                bbox = (minLon, minLat, maxLon, maxLat);
            }
            else
            {
                errors.Add("'bbox' must be 'minLon,minLat,maxLon,maxLat' with min <= max.");
            }
        }

        var page = Page ?? 1;
        if (page < 1)
        {
            errors.Add("'page' must be >= 1.");
        }

        var pageSize = PageSize ?? 25;
        if (pageSize is < 1 or > 200)
        {
            errors.Add("'pageSize' must be between 1 and 200.");
        }

        var sort = Sort?.TrimStart('-') switch
        {
            null or "" or "reportedAt" or "quantity" => Sort,
            _ => ThenAdd(errors, "'sort' must be 'reportedAt' or 'quantity', optionally prefixed with '-'."),
        };

        return new ParsedIncidentQuery
        {
            CountyName = string.IsNullOrWhiteSpace(County) ? null : County.Trim(),
            Region = region,
            Medium = medium,
            Category = category,
            Source = source,
            Status = status,
            From = from,
            To = to,
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
            Bbox = bbox,
            MinGallons = MinGallons,
            HasCoordinates = HasCoordinates,
            Page = page,
            PageSize = pageSize,
            Sort = sort,
            Errors = errors,
        };
    }

    private static string? ThenAdd(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }

    private static TEnum? ParseEnum<TEnum>(string? value, string name, List<string> errors)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(value.Replace(" ", "", StringComparison.Ordinal), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        errors.Add($"'{name}' value '{value}' is not recognized " +
                   $"(expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}).");
        return null;
    }

    private static DateTime? ParseDate(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        errors.Add($"'{name}' value '{value}' is not a recognizable date.");
        return null;
    }
}

public sealed class ParsedIncidentQuery
{
    public string? CountyName { get; init; }
    public EcologyRegion? Region { get; init; }
    public SpillMedium? Medium { get; init; }
    public SubstanceCategory? Category { get; init; }
    public SourceType? Source { get; init; }
    public IncidentStatus? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
    public (double MinLon, double MinLat, double MaxLon, double MaxLat)? Bbox { get; init; }
    public decimal? MinGallons { get; init; }
    public bool? HasCoordinates { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string? Sort { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }

    public bool IsValid => Errors.Count == 0;

    /// <summary>Applies every filter (not paging/sorting) to the incident query.</summary>
    public IQueryable<SpillIncident> ApplyFilters(IQueryable<SpillIncident> incidents)
    {
        if (CountyName is not null)
        {
            incidents = incidents.Where(i => i.County != null && i.County.Name == CountyName);
        }

        if (Region.HasValue)
        {
            incidents = incidents.Where(i => i.County != null && i.County.Region == Region.Value);
        }

        if (Medium.HasValue)
        {
            incidents = incidents.Where(i => i.Medium == Medium.Value);
        }

        if (Category.HasValue)
        {
            incidents = incidents.Where(i => i.SubstanceCategory == Category.Value);
        }

        if (Source.HasValue)
        {
            incidents = incidents.Where(i => i.SourceType == Source.Value);
        }

        if (Status.HasValue)
        {
            incidents = incidents.Where(i => i.Status == Status.Value);
        }

        if (From.HasValue)
        {
            incidents = incidents.Where(i => i.ReportedAtUtc >= From.Value);
        }

        if (To.HasValue)
        {
            incidents = incidents.Where(i => i.ReportedAtUtc < To.Value);
        }

        if (MinGallons.HasValue)
        {
            incidents = incidents.Where(i => i.QuantityGallons >= MinGallons.Value);
        }

        if (HasCoordinates == true)
        {
            incidents = incidents.Where(i => i.Latitude != null && i.Longitude != null);
        }
        else if (HasCoordinates == false)
        {
            incidents = incidents.Where(i => i.Latitude == null || i.Longitude == null);
        }

        if (Bbox is { } box)
        {
            incidents = incidents.Where(i =>
                i.Longitude >= box.MinLon && i.Longitude <= box.MaxLon &&
                i.Latitude >= box.MinLat && i.Latitude <= box.MaxLat);
        }

        if (Search is not null)
        {
            var pattern = $"%{Search}%";
            incidents = incidents.Where(i =>
                EF.Functions.Like(i.SubstanceName, pattern) ||
                EF.Functions.Like(i.Description, pattern) ||
                EF.Functions.Like(i.ResponsibleParty!, pattern) ||
                EF.Functions.Like(i.LocationDescription!, pattern) ||
                EF.Functions.Like(i.WaterbodyName!, pattern) ||
                EF.Functions.Like(i.ReportNumber, pattern));
        }

        return incidents;
    }

    /// <summary>Applies the requested sort. Quantity sorts via double for SQLite compatibility.</summary>
    public IQueryable<SpillIncident> ApplySort(IQueryable<SpillIncident> incidents) => Sort switch
    {
        "reportedAt" => incidents.OrderBy(i => i.ReportedAtUtc),
        "quantity" => incidents.OrderBy(i => (double?)i.QuantityGallons),
        "-quantity" => incidents.OrderByDescending(i => (double?)i.QuantityGallons),
        _ => incidents.OrderByDescending(i => i.ReportedAtUtc),
    };
}
