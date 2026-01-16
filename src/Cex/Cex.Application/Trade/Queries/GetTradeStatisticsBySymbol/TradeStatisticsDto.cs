namespace Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;

public record TradeStatisticsDto
{
    public SideStatisticsDto Buy { get; init; } = new();
    public SideStatisticsDto Sell { get; init; } = new();
}
