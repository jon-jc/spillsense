using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SpillSense.Domain.Intake;
using SpillSense.Infrastructure;
using SpillSense.Infrastructure.Etl;
using SpillSense.Infrastructure.Persistence;
using SpillSense.Web.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpillSenseInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "SpillSense API";
        document.Info.Description =
            "Spill incident data management & analytics: filterable incident queries, " +
            "GeoJSON for mapping, statistical rollups, and import-run auditing.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Apply pending migrations at startup. Fine for a single-node deployment;
// a multi-node deployment would run migrations as a release step instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpillSenseDbContext>();
    await db.Database.MigrateAsync();
}

// CLI mode: `dotnet run -- import <file.csv>` runs the intake pipeline
// against the configured database and exits without starting the server.
if (args is ["import", var csvPath])
{
    // Note: `dotnet run` sets the working directory to the project folder,
    // so relative paths resolve from there; pass an absolute path otherwise.
    if (!File.Exists(csvPath))
    {
        Console.Error.WriteLine($"File not found: {Path.GetFullPath(csvPath)}");
        return 1;
    }

    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<IncidentImportService>();

    using var reader = new StreamReader(csvPath);
    var run = await importer.ImportAsync(Path.GetFileName(csvPath), reader);

    Console.WriteLine(
        $"{run.Status}: {run.TotalRows} rows - {run.InsertedCount} inserted, " +
        $"{run.UpdatedCount} updated, {run.UnchangedCount} unchanged, {run.RejectedCount} quarantined.");
    return run.Status == ImportRunStatus.Failed ? 1 : 0;
}

app.MapGet("/healthz", async (SpillSenseDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Problem("Database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable);
}).WithTags("Health");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapIncidentEndpoints();
app.MapStatsEndpoints();
app.MapImportEndpoints();
app.MapCountyEndpoints();

app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("SpillSense API"));

app.Run();
return 0;

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
