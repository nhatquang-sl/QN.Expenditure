using Cex.Domain.Entities;
using Cex.Domain.Enums;

namespace Cex.Application.Signals.Commands.FindSignal;

public class FoundSignalEvent
{
    // Parameterless constructor required for MassTransit deserialization.
    // System.Text.Json binds the constructor parameter 'signal' (Signal type) and fails
    // when it can't find a matching JSON property. The parameterless constructor lets
    // the deserializer use property binding instead.
    public FoundSignalEvent() { }

    public FoundSignalEvent(Signal signal)
    {
        SignalId = signal.Id;
        Symbol = signal.Symbol;
        Interval = signal.Interval;
        SignalType = signal.SignalType;
        DetectedAt = signal.DetectedAt;
        PreviousCandleAt = signal.PreviousCandleAt;
        RsiValue = signal.RsiValue;
        PreviousRsiValue = signal.PreviousRsiValue;
        EntryPrice = signal.EntryPrice;
        StopLoss = signal.StopLoss;
        TakeProfit = signal.TakeProfit;
        Leverage = signal.Leverage;
    }

    public int SignalId { get; set; }
    public string Symbol { get; set; } = default!;
    public string Interval { get; set; } = default!;
    public SignalType SignalType { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime PreviousCandleAt { get; set; }
    public decimal RsiValue { get; set; }
    public decimal PreviousRsiValue { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public int Leverage { get; set; }
}