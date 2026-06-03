using Cex.Domain.Enums;

namespace Cex.Domain.Entities;

public class Signal
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public SignalType SignalType { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime PreviousCandleAt { get; set; }
    public decimal RsiValue { get; set; }
    public decimal PreviousRsiValue { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public int Leverage { get; set; } = 10;
    public decimal MaxProfit { get; set; } = 0;
    public DateTime? MaxProfitHitAt { get; set; }
    public DateTime? MaxProfitCheckedAt { get; set; }
    public DateTime? EntryHitAt { get; set; }
    public DateTime? StopLossHitAt { get; set; }
    public DateTime? TakeProfitHitAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheckedCandleAt { get; set; }
    public int EntryHitAfterMinutes { get; set; } = -1;
    public int MaxProfitHitAfterMinutes { get; set; } = -1;
    public int StopLossHitAfterMinutes { get; set; } = -1;
}
