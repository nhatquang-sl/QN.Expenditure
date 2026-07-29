using Cex.Domain.Entities;
using Cex.Domain.Enums;

namespace Cex.Application.Signals.Commands.FindSignal;

public class FoundSignalEvent(Signal signal)
{
    public int SignalId { get; set; } = signal.Id;
    public string Symbol { get; set; } = signal.Symbol;
    public string Interval { get; set; } = signal.Interval;
    public SignalType SignalType { get; set; } = signal.SignalType;
    public DateTime DetectedAt { get; set; } = signal.DetectedAt;
    public DateTime PreviousCandleAt { get; set; } = signal.PreviousCandleAt;
    public decimal RsiValue { get; set; } = signal.RsiValue;
    public decimal PreviousRsiValue { get; set; } = signal.PreviousRsiValue;
    public decimal EntryPrice { get; set; } = signal.EntryPrice;
    public decimal StopLoss { get; set; } = signal.StopLoss;
    public decimal TakeProfit { get; set; } = signal.TakeProfit;
    public int Leverage { get; set; } = signal.Leverage;
}