using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class SpotOrderConfiguration : IEntityTypeConfiguration<SpotOrder>
    {
        public void Configure(EntityTypeBuilder<SpotOrder> builder)
        {
            builder.HasKey(t => t.OrderId);
            builder.Property(t => t.OrderId).ValueGeneratedNever();

            builder.HasOne(t => t.SyncSetting)
                .WithMany(t => t.SpotOrders)
                .HasForeignKey(t => new { t.Symbol, t.UserId })
                .OnDelete(DeleteBehavior.Cascade);

            // Guid max length
            builder.Property(t => t.UserId)
                .HasMaxLength(36);

            builder.Property(t => t.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.ClientOrderId)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(t => t.Price)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.OrigQty)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.ExecutedQty)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.CummulativeQuoteQty)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.TimeInForce)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.Type)
                .HasMaxLength(25)
                .IsRequired();

            builder.Property(t => t.Side)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.StopPrice)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.IcebergQty)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.OrigQuoteOrderQty)
                .HasPrecision(18, 8)
                .IsRequired();

            builder.Property(t => t.SelfTradePreventionMode)
                .HasMaxLength(20)
                .IsRequired();
        }
    }
}
