using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpillSense.Domain.Intake;

namespace SpillSense.Infrastructure.Persistence.Configurations;

public class QuarantinedRecordConfiguration : IEntityTypeConfiguration<QuarantinedRecord>
{
    public void Configure(EntityTypeBuilder<QuarantinedRecord> builder)
    {
        builder.ToTable("QuarantinedRecords");

        builder.Property(q => q.ReportNumber).HasMaxLength(32);
        builder.Property(q => q.RawRow).HasMaxLength(8000).IsRequired();
        builder.Property(q => q.Reasons).HasMaxLength(4000).IsRequired();

        builder.HasOne(q => q.ImportRun)
            .WithMany(r => r.QuarantinedRecords)
            .HasForeignKey(q => q.ImportRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.ImportRunId);
    }
}
