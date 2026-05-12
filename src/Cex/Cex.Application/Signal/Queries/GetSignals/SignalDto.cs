namespace Cex.Application.Signals.Queries.GetSignals;

public record SignalDto
{
    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public string SignalType { get; init; } = string.Empty;
    public DateTime DetectedAt { get; init; }
    public decimal RsiValue { get; init; }
    public decimal PreviousRsiValue { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal TakeProfit { get; init; }
    public int Leverage { get; init; }
    public decimal MaxProfit { get; init; }
    public DateTime? MaxProfitHitAt { get; init; }
    public DateTime? EntryHitAt { get; init; }
    public DateTime? StopLossHitAt { get; init; }
    public DateTime? TakeProfitHitAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
