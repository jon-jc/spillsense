using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Web.Api;

public static class CountyEndpoints
{
    public static IEndpointRouteBuilder MapCountyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/counties", ListAsync)
            .WithTags("Reference data")
            .WithSummary("All 39 Washington counties with Ecology region assignments");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<CountyDto>>> ListAsync(
        SpillSenseDbContext db, CancellationToken cancellationToken)
    {
        var counties = await db.Counties.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CountyDto(c.Name, c.FipsCode, c.Region.ToString(), c.IsCoastal))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok((IReadOnlyList<CountyDto>)counties);
    }
}

public sealed record CountyDto(string Name, string FipsCode, string Region, bool IsCoastal);
