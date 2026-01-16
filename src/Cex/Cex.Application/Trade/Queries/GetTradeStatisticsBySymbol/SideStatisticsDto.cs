namespace Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;

public record SideStatisticsDto
{
    public decimal TotalFunds { get; init; }
    public decimal TotalFee { get; init; }
    public decimal TotalSize { get; init; }
    public decimal AvgPrice { get; init; }
}
