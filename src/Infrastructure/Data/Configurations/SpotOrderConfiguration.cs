using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class SpotOrderConfiguration : IEntityTypeConfiguration<SpotOrder>
    {
        public void Configure(EntityTypeBuilder<SpotOrder> builder)
        {
            // Guid max length
            builder.HasKey(t => t.OrderId);

            builder.Property(t => t.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.ClientOrderId)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(t => t.Price)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.OrigQty)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.ExecutedQty)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.CummulativeQuoteQty)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.TimeInForce)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.Type)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.Side)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.StopPrice)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.IcebergQty)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.OrigQuoteOrderQty)
                .HasPrecision(10, 8)
                .IsRequired();

            builder.Property(t => t.SelfTradePreventionMode)
                .HasMaxLength(10)
                .IsRequired();
        }
    }
}
