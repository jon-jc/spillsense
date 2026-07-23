using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class IncidentEndpoints
{
    /// <summary>Hard cap on features returned by the GeoJSON endpoint.</summary>
    private const int GeoJsonLimit = 5000;

    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/incidents").WithTags("Incidents");

        group.MapGet("/", ListAsync)
            .WithSummary("List incidents")
            .WithDescription("Filterable, paged incident list. All filters combine with AND.");

        group.MapGet("/geojson", GeoJsonAsync)
            .WithSummary("Incidents as GeoJSON")
            .WithDescription("Same filters as the list endpoint; returns a FeatureCollection " +
                             "of incidents that carry coordinates (capped at 5000 features).");

        group.MapGet("/{reportNumber}", GetByReportNumberAsync)
            .WithSummary("Get one incident by report number");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<IncidentSummaryDto>>, ValidationProblem>> ListAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return Problems(parsed.Errors);
        }

        var filtered = parsed.ApplyFilters(db.Incidents.AsNoTracking().Include(i => i.County));
        var total = await filtered.CountAsync(cancellationToken);

        var items = await parsed.ApplySort(filtered)
            .Skip((parsed.Page - 1) * parsed.PageSize)
            .Take(parsed.PageSize)
            .Select(i => i.ToSummaryDto())
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedResult<IncidentSummaryDto>(total, parsed.Page, parsed.PageSize, items));
    }

    private static async Task<Results<Ok<IncidentDetailDto>, NotFound>> GetByReportNumberAsync(
        string reportNumber, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var incident = await db.Incidents.AsNoTracking()
            .Include(i => i.County)
            .FirstOrDefaultAsync(i => i.ReportNumber == reportNumber, cancellationToken);

        return incident is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(incident.ToDetailDto());
    }

    private static async Task<Results<Ok<GeoJsonFeatureCollection>, ValidationProblem>> GeoJsonAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return Problems(parsed.Errors);
        }

        var incidents = await parsed
            .ApplyFilters(db.Incidents.AsNoTracking().Include(i => i.County))
            .Where(i => i.Latitude != null && i.Longitude != null)
            .OrderByDescending(i => i.ReportedAtUtc)
            .Take(GeoJsonLimit)
            .ToListAsync(cancellationToken);

        var features = incidents.Select(i => new GeoJsonFeature(
            Geometry: new GeoJsonPoint([i.Longitude!.Value, i.Latitude!.Value]),
            Properties: i.ToSummaryDto()));

        return TypedResults.Ok(new GeoJsonFeatureCollection([.. features]));
    }

    private static ValidationProblem Problems(IReadOnlyList<string> errors) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [.. errors] });
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "'FeatureCollection' is the GeoJSON type name defined by RFC 7946.")]
public sealed record GeoJsonFeatureCollection(IReadOnlyList<GeoJsonFeature> Features)
{
    public string Type => "FeatureCollection";
}

public sealed record GeoJsonFeature(GeoJsonPoint Geometry, IncidentSummaryDto Properties)
{
    public string Type => "Feature";
}

/// <summary>GeoJSON position order is [longitude, latitude] per RFC 7946.</summary>
public sealed record GeoJsonPoint(IReadOnlyList<double> Coordinates)
{
    public string Type => "Point";
}
