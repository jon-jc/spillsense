using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Domain.Incidents;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/annual/{year:int}", AnnualAsync)
            .WithTags("Reports")
            .WithSummary("Annual program report")
            .WithDescription("Composed rollup for one calendar year: totals, quarterly breakdown, " +
                             "top counties and substances by volume, largest incidents, and " +
                             "year-over-year change. Shaped for report tooling (SSRS-style) and briefings.");

        return app;
    }

    private static async Task<Results<Ok<AnnualReportDto>, ValidationProblem>> AnnualAsync(
        int year, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        if (year is < 1990 or > 2100)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["year"] = ["'year' must be between 1990 and 2100."] });
        }

        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddYears(1);
        var incidents = db.Incidents.AsNoTracking()
            .Where(i => i.ReportedAtUtc >= start && i.ReportedAtUtc < end);

        var totals = await incidents
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
                Recovered = g.Sum(i => (double?)i.RecoveredGallons) ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var quarters = await incidents
            .GroupBy(i => (i.ReportedAtUtc.Month - 1) / 3 + 1)
            .Select(g => new
            {
                Quarter = g.Key,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderBy(q => q.Quarter)
            .ToListAsync(cancellationToken);

        var topCountyRows = await incidents
            .Where(i => i.CountyId != null)
            .GroupBy(i => i.County!.Name)
            .Select(g => new
            {
                Name = g.Key,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderByDescending(b => b.Count)
            .Take(5)
            .ToListAsync(cancellationToken);
        var topCounties = topCountyRows
            .Select(c => new AnnualBucketDto(c.Name, c.Count, Math.Round(c.Gallons, 1)))
            .ToList();

        var topSubstances = await incidents
            .GroupBy(i => i.SubstanceCategory)
            .Select(g => new
            {
                Category = g.Key,
                Count = g.Count(),
                Gallons = g.Sum(i => (double?)i.QuantityGallons) ?? 0,
            })
            .OrderByDescending(b => b.Gallons)
            .Take(5)
            .ToListAsync(cancellationToken);

        var largest = await incidents
            .Include(i => i.County)
            .Where(i => i.QuantityGallons != null)
            .OrderByDescending(i => (double?)i.QuantityGallons)
            .Take(5)
            .ToListAsync(cancellationToken);

        var previousYearCount = await db.Incidents.AsNoTracking()
            .CountAsync(i => i.ReportedAtUtc >= start.AddYears(-1) && i.ReportedAtUtc < start,
                cancellationToken);

        var count = totals?.Count ?? 0;
        var gallons = Math.Round(totals?.Gallons ?? 0, 1);
        var recovered = Math.Round(totals?.Recovered ?? 0, 1);

        return TypedResults.Ok(new AnnualReportDto(
            year,
            count,
            gallons,
            recovered,
            gallons > 0 ? Math.Round(recovered / gallons * 100, 1) : 0,
            [.. quarters.Select(q => new QuarterDto($"Q{q.Quarter}", q.Count, Math.Round(q.Gallons, 1)))],
            topCounties,
            [.. topSubstances.Select(s => new AnnualBucketDto(s.Category.ToString(), s.Count, Math.Round(s.Gallons, 1)))],
            [.. largest.Select(i => i.ToSummaryDto())],
            previousYearCount,
            previousYearCount > 0
                ? Math.Round((count - previousYearCount) / (double)previousYearCount * 100, 1)
                : null));
    }
}

public sealed record AnnualReportDto(
    int Year,
    int TotalIncidents,
    double TotalGallons,
    double RecoveredGallons,
    double RecoveryRatePercent,
    IReadOnlyList<QuarterDto> ByQuarter,
    IReadOnlyList<AnnualBucketDto> TopCountiesByCount,
    IReadOnlyList<AnnualBucketDto> TopSubstancesByVolume,
    IReadOnlyList<IncidentSummaryDto> LargestIncidents,
    int PreviousYearIncidents,
    double? YearOverYearChangePercent);

public sealed record QuarterDto(string Quarter, int Count, double Gallons);

public sealed record AnnualBucketDto(string Key, int Count, double Gallons);
