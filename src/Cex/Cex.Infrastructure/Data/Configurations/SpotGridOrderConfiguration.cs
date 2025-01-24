using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class SpotGridOrderConfiguration : IEntityTypeConfiguration<SpotGridStep>
    {
        public void Configure(EntityTypeBuilder<SpotGridStep> builder)
        {
            builder.Property(t => t.BuyPrice)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.SellPrice)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Qty)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (SpotGridStepStatus)Enum.Parse(typeof(SpotGridStepStatus), v))
                .HasMaxLength(16)
                .IsRequired();
        }
    }
}