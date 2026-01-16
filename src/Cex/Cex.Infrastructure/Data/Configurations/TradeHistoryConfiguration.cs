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

            builder.Property(t => t.UserId)
                .IsRequired();

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

            // Index for pagination query (GetTradeHistoriesBySymbol)
            builder.HasIndex(x => new { x.UserId, x.Symbol, x.TradedAt })
                .HasDatabaseName("IX_TradeHistories_UserId_Symbol_TradedAt")
                .IsDescending(false, false, true);  // UserId ASC, Symbol ASC, TradedAt DESC

            // Covering index for statistics query (GetTradeStatisticsBySymbol)
            builder.HasIndex(x => new { x.UserId, x.Symbol, x.Side })
                .HasDatabaseName("IX_TradeHistories_Stats")
                .IncludeProperties(x => new { x.Funds, x.Fee, x.Size });
        }
    }
}