using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class SpotGridConfiguration : IEntityTypeConfiguration<SpotGrid>
    {
        public void Configure(EntityTypeBuilder<SpotGrid> builder)
        {
            builder.HasQueryFilter(t => t.DeletedAt == null);

            builder.Property(t => t.LowerPrice)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.UpperPrice)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.TriggerPrice)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Investment)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.BaseBalance)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.QuoteBalance)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Profit)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.TakeProfit)
                .HasPrecision(13, 6);

            builder.Property(t => t.StopLoss)
                .HasPrecision(13, 6);
        }
    }
}