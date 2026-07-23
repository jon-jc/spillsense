using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpillSense.Domain.Incidents;

namespace SpillSense.Infrastructure.Persistence.Configurations;

public class SpillIncidentConfiguration : IEntityTypeConfiguration<SpillIncident>
{
    public void Configure(EntityTypeBuilder<SpillIncident> builder)
    {
        builder.ToTable("SpillIncidents");

        builder.Property(i => i.ReportNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(i => i.ReportNumber).IsUnique();

        builder.Property(i => i.Description).HasMaxLength(4000).IsRequired();
        builder.Property(i => i.LocationDescription).HasMaxLength(500);
        builder.Property(i => i.WaterbodyName).HasMaxLength(200);
        builder.Property(i => i.SubstanceName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ResponsibleParty).HasMaxLength(300);

        builder.Property(i => i.QuantityGallons).HasPrecision(14, 2);
        builder.Property(i => i.RecoveredGallons).HasPrecision(14, 2);

        // Enums stored as strings keeps the database self-describing for
        // report writers querying outside the application.
        builder.Property(i => i.Medium).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.SubstanceCategory).HasConversion<string>().HasMaxLength(30);
        builder.Property(i => i.SourceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(i => i.County)
            .WithMany()
            .HasForeignKey(i => i.CountyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query paths used by the dashboard and reporting endpoints.
        builder.HasIndex(i => i.ReportedAtUtc);
        builder.HasIndex(i => i.CountyId);
        builder.HasIndex(i => i.SubstanceCategory);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => new { i.Latitude, i.Longitude });

        builder.Ignore(i => i.HasCoordinates);
    }
}
