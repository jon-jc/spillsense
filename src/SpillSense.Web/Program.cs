using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure;
using SpillSense.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpillSenseInfrastructure(builder.Configuration);

var app = builder.Build();

// Apply pending migrations at startup. Fine for a single-node deployment;
// a multi-node deployment would run migrations as a release step instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpillSenseDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/healthz", async (SpillSenseDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Problem("Database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
