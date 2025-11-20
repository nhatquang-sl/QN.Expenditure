using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class TradeHistoryConfiguration : IEntityTypeConfiguration<TradeHistory>
    {
        public void Configure(EntityTypeBuilder<TradeHistory> builder)
        {
            builder.HasKey(k => new { k.UserId, k.TradeId });

            builder.Property(t => t.Price)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Size)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Funds)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.Fee)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.FeeRate)
                .HasPrecision(13, 6)
                .IsRequired();

            builder.Property(t => t.CreatedAt).HasDefaultValue(DateTime.UtcNow);
        }
    }
}