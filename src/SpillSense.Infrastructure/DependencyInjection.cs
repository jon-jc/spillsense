using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpillSense.Infrastructure.Etl;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the persistence layer. SQLite is the default provider so the
    /// project runs anywhere with zero setup; the schema and EF model are kept
    /// provider-portable so a SQL Server connection string is a drop-in swap.
    /// </summary>
    public static IServiceCollection AddSpillSenseInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SpillSense")
            ?? "Data Source=spillsense.db";

        services.AddDbContext<SpillSenseDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IncidentImportService>();

        return services;
    }
}
