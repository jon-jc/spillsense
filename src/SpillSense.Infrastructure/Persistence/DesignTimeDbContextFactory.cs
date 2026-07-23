using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpillSense.Infrastructure.Persistence;

/// <summary>
/// Lets the EF Core CLI (`dotnet ef`) create the context without booting the web host.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SpillSenseDbContext>
{
    public SpillSenseDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SpillSenseDbContext>()
            .UseSqlite("Data Source=spillsense.db")
            .Options;
        return new SpillSenseDbContext(options);
    }
}
