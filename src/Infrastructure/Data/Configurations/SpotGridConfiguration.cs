using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class SpotGridConfiguration : IEntityTypeConfiguration<SpotGrid>
    {
        public void Configure(EntityTypeBuilder<SpotGrid> builder)
        {
            builder.Property(t => t.UserId)
                .HasMaxLength(36)
                .IsRequired();

            builder.Property(t => t.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.LowerPrice)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.UpperPrice)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.TriggerPrice)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.Investment)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.TakeProfit)
                .HasPrecision(18, 8);

            builder.Property(t => t.StopLoss)
                .HasPrecision(18, 8);

            builder.Property(t => t.Status)
                .HasConversion<string>();

            builder.Property(t => t.GridMode)
                .HasConversion<string>();
        }
    }
}
