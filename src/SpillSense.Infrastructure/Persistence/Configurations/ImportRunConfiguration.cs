using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpillSense.Domain.Intake;

namespace SpillSense.Infrastructure.Persistence.Configurations;

public class ImportRunConfiguration : IEntityTypeConfiguration<ImportRun>
{
    public void Configure(EntityTypeBuilder<ImportRun> builder)
    {
        builder.ToTable("ImportRuns");

        builder.Property(r => r.SourceName).HasMaxLength(260).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.FailureReason).HasMaxLength(2000);

        builder.HasIndex(r => r.StartedAtUtc);
    }
}
