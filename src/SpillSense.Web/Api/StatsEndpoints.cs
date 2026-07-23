using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stats").WithTags("Statistics");

        group.MapGet("/summary", SummaryAsync)
            .WithSummary("Aggregate summary")
            .WithDescription("Counts and volumes rolled up by medium, substance category, " +
                             "source, and status. Accepts the same filters as the incident list.");

        group.MapGet("/trend", TrendAsync)
            .WithSummary("Monthly trend")
            .WithDescription("Incident counts and spilled volume per calendar month.");

        group.MapGet("/counties", CountiesAsync)
            .WithSummary("Per-county rollup")
            .WithDescription("Incident count and volume for every county with at least one incident.");

        return app;
    }

    private static async Task<Results<Ok<StatsSummaryDto>, ValidationProblem>> SummaryAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return Problems(parsed.Errors);
        }

        var filtered = parsed.ApplyFilters(db.Incidents.AsNoTracking());

        // Aggregate via double: the SQLite provider cannot translate decimal SUM.
        var totals = await filtered
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
                Recovered = g.Sum(i => (double?)i.RecoveredGallons) ?? 0,
                WithCoordinates = g.Count(i => i.Latitude != null && i.Longitude != null),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var byMedium = await BucketAsync(filtered, i => i.Medium.ToString(), cancellationToken);
        var byCategory = await BucketAsync(filtered, i => i.SubstanceCategory.ToString(), cancellationToken);
        var bySource = await BucketAsync(filtered, i => i.SourceType.ToString(), cancellationToken);
        var byStatus = await BucketAsync(filtered, i => i.Status.ToString(), cancellationToken);

        return TypedResults.Ok(new StatsSummaryDto(
            totals?.Count ?? 0,
            Math.Round(totals?.Gallons ?? 0, 1),
            Math.Round(totals?.Recovered ?? 0, 1),
            totals?.WithCoordinates ?? 0,
            byMedium, byCategory, bySource, byStatus));
    }

    private static async Task<Results<Ok<IReadOnlyList<TrendPointDto>>, ValidationProblem>> TrendAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return Problems(parsed.Errors);
        }

        var points = await parsed.ApplyFilters(db.Incidents.AsNoTracking())
            .GroupBy(i => new { i.ReportedAtUtc.Year, i.ReportedAtUtc.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToListAsync(cancellationToken);

        IReadOnlyList<TrendPointDto> dtos = [.. points.Select(p =>
            new TrendPointDto($"{p.Year:D4}-{p.Month:D2}", p.Count, Math.Round(p.Gallons, 1)))];
        return TypedResults.Ok(dtos);
    }

    private static async Task<Results<Ok<IReadOnlyList<CountyStatsDto>>, ValidationProblem>> CountiesAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return Problems(parsed.Errors);
        }

        var rows = await parsed.ApplyFilters(db.Incidents.AsNoTracking())
            .Where(i => i.CountyId != null)
            .GroupBy(i => new { i.County!.Name, i.County.Region, i.County.FipsCode })
            .Select(g => new
            {
                g.Key.Name,
                g.Key.Region,
                g.Key.FipsCode,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderByDescending(r => r.Count)
            .ToListAsync(cancellationToken);

        IReadOnlyList<CountyStatsDto> dtos = [.. rows.Select(r =>
            new CountyStatsDto(r.Name, r.Region.ToString(), r.FipsCode, r.Count, Math.Round(r.Gallons, 1)))];
        return TypedResults.Ok(dtos);
    }

    private static async Task<IReadOnlyList<BucketDto>> BucketAsync(
        IQueryable<SpillSense.Domain.Incidents.SpillIncident> incidents,
        System.Linq.Expressions.Expression<Func<SpillSense.Domain.Incidents.SpillIncident, string>> keySelector,
        CancellationToken cancellationToken)
    {
        var rows = await incidents
            .GroupBy(keySelector)
            .Select(g => new
            {
                Key = g.Key,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderByDescending(b => b.Count)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new BucketDto(r.Key, r.Count, Math.Round(r.Gallons, 1)))];
    }

    private static ValidationProblem Problems(IReadOnlyList<string> errors) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [.. errors] });
}

public sealed record StatsSummaryDto(
    int TotalIncidents,
    double TotalGallons,
    double RecoveredGallons,
    int WithCoordinates,
    IReadOnlyList<BucketDto> ByMedium,
    IReadOnlyList<BucketDto> ByCategory,
    IReadOnlyList<BucketDto> BySource,
    IReadOnlyList<BucketDto> ByStatus);

public sealed record BucketDto(string Key, int Count, double Gallons);

public sealed record TrendPointDto(string Month, int Count, double Gallons);

public sealed record CountyStatsDto(string County, string Region, string FipsCode, int Count, double Gallons);
