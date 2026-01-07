namespace Cex.Application.Trade.Queries.GetTradeHistoriesBySymbol;

public record TradeHistoryDto
{
    public string Symbol { get; init; } = string.Empty;
    public long TradeId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Size { get; init; }
    public decimal Funds { get; init; }
    public decimal Fee { get; init; }
    public string FeeCurrency { get; init; } = string.Empty;
    public DateTime TradedAt { get; init; }

    public decimal Total => Funds + Fee;
}
