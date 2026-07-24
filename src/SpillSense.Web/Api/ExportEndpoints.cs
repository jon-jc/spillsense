using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents/export", ExportAsync)
            .WithTags("Incidents")
            .WithSummary("Export incidents as CSV")
            .WithDescription("Streams the filtered incident set as a CSV attachment. " +
                             "Accepts the same filters as the incident list.");

        return app;
    }

    private static async Task<IResult> ExportAsync(
        [AsParameters] IncidentQuery query, SpillSenseDbContext db, HttpContext http)
    {
        var parsed = query.Parse();
        if (!parsed.IsValid)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["query"] = [.. parsed.Errors] });
        }

        var incidents = parsed
            .ApplySort(parsed.ApplyFilters(db.Incidents.AsNoTracking().Include(i => i.County)))
            .AsAsyncEnumerable();

        http.Response.ContentType = "text/csv; charset=utf-8";
        http.Response.Headers.ContentDisposition =
            $"attachment; filename=spillsense-incidents-{DateTime.UtcNow:yyyyMMdd}.csv";

        await using var writer = new StreamWriter(http.Response.Body);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in new[]
        {
            "ReportNumber", "ReportedAtUtc", "OccurredAtUtc", "County", "EcologyRegion",
            "Medium", "SubstanceName", "SubstanceCategory", "QuantityGallons", "RecoveredGallons",
            "SourceType", "Status", "Latitude", "Longitude", "LocationDescription",
            "WaterbodyName", "ResponsibleParty", "Description",
        })
        {
            csv.WriteField(header);
        }
        await csv.NextRecordAsync();

        await foreach (var i in incidents)
        {
            csv.WriteField(i.ReportNumber);
            csv.WriteField(i.ReportedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            csv.WriteField(i.OccurredAtUtc?.ToString("O", CultureInfo.InvariantCulture));
            csv.WriteField(i.County?.Name);
            csv.WriteField(i.County?.Region.ToString());
            csv.WriteField(i.Medium.ToString());
            csv.WriteField(i.SubstanceName);
            csv.WriteField(i.SubstanceCategory.ToString());
            csv.WriteField(i.QuantityGallons);
            csv.WriteField(i.RecoveredGallons);
            csv.WriteField(i.SourceType.ToString());
            csv.WriteField(i.Status.ToString());
            csv.WriteField(i.Latitude);
            csv.WriteField(i.Longitude);
            csv.WriteField(i.LocationDescription);
            csv.WriteField(i.WaterbodyName);
            csv.WriteField(i.ResponsibleParty);
            csv.WriteField(i.Description);
            await csv.NextRecordAsync();
        }

        return TypedResults.Empty;
    }
}
