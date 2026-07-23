using Microsoft.EntityFrameworkCore;
using SpillSense.Domain.Geography;
using SpillSense.Domain.Incidents;

namespace SpillSense.Infrastructure.Persistence;

public class SpillSenseDbContext : DbContext
{
    public SpillSenseDbContext(DbContextOptions<SpillSenseDbContext> options)
        : base(options)
    {
    }

    public DbSet<SpillIncident> Incidents => Set<SpillIncident>();
    public DbSet<County> Counties => Set<County>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpillSenseDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<SpillIncident>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
