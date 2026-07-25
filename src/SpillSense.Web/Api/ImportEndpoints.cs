using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Etl;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class ImportEndpoints
{
    /// <summary>Upper bound on uploaded CSV size. Generous: 20 MB is ~100k rows.</summary>
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").WithTags("Imports");

        group.MapGet("/", ListAsync)
            .WithSummary("List import runs, newest first");

        group.MapGet("/{id:int}/quarantine", QuarantineAsync)
            .WithSummary("Quarantined rows for an import run");

        // Cookie-less JSON/file API: CSRF tokens do not apply here, and the
        // antiforgery middleware would otherwise reject the multipart post.
        group.MapPost("/", UploadAsync)
            .WithSummary("Import an incident CSV file")
            .WithDescription("Runs the intake pipeline on an uploaded CSV: rows are validated, " +
                             "upserts are idempotent by report number, and rejected rows are " +
                             "quarantined. Returns the completed import run with its counts.")
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<ImportRunDto>, ValidationProblem>> UploadAsync(
        IFormFile? file, IncidentImportService importer, CancellationToken cancellationToken)
    {
        static ValidationProblem Problem(string message) =>
            TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

        if (file is null || file.Length == 0)
        {
            return Problem("Attach a non-empty CSV file in the 'file' form field.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return Problem($"File exceeds the {MaxUploadBytes / (1024 * 1024)} MB upload limit.");
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Problem("Only .csv files are accepted.");
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var run = await importer.ImportAsync(file.FileName, reader, cancellationToken);
        return TypedResults.Ok(run.ToDto());
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
