namespace Cex.Application.Signals.Queries.GetSignals;

public record SignalDto
{
    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public string SignalType { get; init; } = string.Empty;
    public long DetectedAt { get; init; }
    public decimal RsiValue { get; init; }
    public decimal PreviousRsiValue { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal TakeProfit { get; init; }
    public int Leverage { get; init; }
    public decimal MaxProfit { get; init; }
    public long? MaxProfitHitAt { get; init; }
    public long? EntryHitAt { get; init; }
    public long? StopLossHitAt { get; init; }
    public long? TakeProfitHitAt { get; init; }
    public long CreatedAt { get; init; }
    public int EntryHitAfterMinutes { get; init; }
    public int MaxProfitHitAfterMinutes { get; init; }
    public int StopLossHitAfterMinutes { get; init; }
}
