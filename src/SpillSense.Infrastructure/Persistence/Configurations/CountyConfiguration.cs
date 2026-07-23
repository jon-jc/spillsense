using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpillSense.Domain.Geography;

namespace SpillSense.Infrastructure.Persistence.Configurations;

public class CountyConfiguration : IEntityTypeConfiguration<County>
{
    public void Configure(EntityTypeBuilder<County> builder)
    {
        builder.ToTable("Counties");

        builder.Property(c => c.Name).HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.FipsCode).HasMaxLength(5).IsRequired();
        builder.HasIndex(c => c.FipsCode).IsUnique();

        builder.Property(c => c.Region).HasConversion<string>().HasMaxLength(20);

        builder.HasData(WashingtonCounties.All);
    }
}
