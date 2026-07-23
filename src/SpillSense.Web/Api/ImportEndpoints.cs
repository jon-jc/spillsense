using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").WithTags("Imports");

        group.MapGet("/", ListAsync)
            .WithSummary("List import runs, newest first");

        group.MapGet("/{id:int}/quarantine", QuarantineAsync)
            .WithSummary("Quarantined rows for an import run");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ImportRunDto>>> ListAsync(
        SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var runs = await db.ImportRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ImportRunDto> dtos = [.. runs.Select(r => r.ToDto())];
        return TypedResults.Ok(dtos);
    }

    private static async Task<Results<Ok<IReadOnlyList<QuarantinedRecordDto>>, NotFound>> QuarantineAsync(
        int id, SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var runExists = await db.ImportRuns.AsNoTracking().AnyAsync(r => r.Id == id, cancellationToken);
        if (!runExists)
        {
            return TypedResults.NotFound();
        }

        var records = await db.QuarantinedRecords.AsNoTracking()
            .Where(q => q.ImportRunId == id)
            .OrderBy(q => q.RowNumber)
            .ToListAsync(cancellationToken);

        IReadOnlyList<QuarantinedRecordDto> dtos = [.. records.Select(q => q.ToDto())];
        return TypedResults.Ok(dtos);
    }
}
