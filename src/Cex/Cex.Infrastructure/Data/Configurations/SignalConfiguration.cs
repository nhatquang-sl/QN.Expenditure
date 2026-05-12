using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations;

public class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> builder)
    {
        builder.ToTable("Signals", t =>
            t.HasCheckConstraint("CK_Signals_Leverage", "[Leverage] >= 1 AND [Leverage] <= 125"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignalType).HasConversion<string>();
        builder.Property(x => x.Symbol).HasMaxLength(20);
        builder.Property(x => x.Interval).HasMaxLength(20);
        builder.Property(x => x.RsiValue).HasPrecision(10, 4);
        builder.Property(x => x.PreviousRsiValue).HasPrecision(10, 4);
        builder.Property(x => x.EntryPrice).HasPrecision(18, 8);
        builder.Property(x => x.StopLoss).HasPrecision(18, 8);
        builder.Property(x => x.TakeProfit).HasPrecision(18, 8);
        builder.Property(x => x.Leverage).HasDefaultValue(10);
        builder.Property(x => x.MaxProfit).HasPrecision(10, 4).HasDefaultValue(0m);
        builder.Property(x => x.MaxProfitHitAt).HasPrecision(0);
        builder.Property(x => x.MaxProfitCheckedAt).HasPrecision(0);
        builder.Property(x => x.DetectedAt).HasPrecision(0);
        builder.Property(x => x.PreviousCandleAt).HasPrecision(0);
        builder.Property(x => x.EntryHitAt).HasPrecision(0);
        builder.Property(x => x.StopLossHitAt).HasPrecision(0);
        builder.Property(x => x.TakeProfitHitAt).HasPrecision(0);
        builder.Property(x => x.CreatedAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.LastCheckedCandleAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");

        // Uniqueness guard: one signal per symbol + interval + candle time
        builder.HasIndex(x => new { x.Symbol, x.Interval, x.DetectedAt }).IsUnique();
        builder.HasIndex(x => x.LastCheckedCandleAt);
        builder.HasIndex(x => x.MaxProfitCheckedAt);
    }
}
