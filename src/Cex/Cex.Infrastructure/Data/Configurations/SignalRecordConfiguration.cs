using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations;

public class SignalRecordConfiguration : IEntityTypeConfiguration<SignalRecord>
{
    public void Configure(EntityTypeBuilder<SignalRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignalType).HasConversion<string>();
        builder.Property(x => x.Symbol).HasMaxLength(20);
        builder.Property(x => x.Interval).HasMaxLength(20);
        builder.Property(x => x.RsiValue).HasPrecision(10, 4);
        builder.Property(x => x.PreviousRsiValue).HasPrecision(10, 4);
        builder.Property(x => x.EntryPrice).HasPrecision(18, 8);
        builder.Property(x => x.StopLoss).HasPrecision(18, 8);
        builder.Property(x => x.TakeProfit).HasPrecision(18, 8);
        builder.Property(x => x.DetectedAt).HasPrecision(0);
        builder.Property(x => x.PreviousCandleAt).HasPrecision(0);
        builder.Property(x => x.EntryHitAt).HasPrecision(0);
        builder.Property(x => x.StopLossHitAt).HasPrecision(0);
        builder.Property(x => x.TakeProfitHitAt).HasPrecision(0);
        builder.Property(x => x.CreatedAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.LastCheckedCandleAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");

        // Uniqueness guard: one signal record per symbol + interval + candle time
        builder.HasIndex(x => new { x.Symbol, x.Interval, x.DetectedAt }).IsUnique();
        builder.HasIndex(x => x.LastCheckedCandleAt);
    }
}
