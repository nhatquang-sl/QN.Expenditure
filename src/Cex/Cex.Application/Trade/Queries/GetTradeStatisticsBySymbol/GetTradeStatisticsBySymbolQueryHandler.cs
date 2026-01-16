using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;

public class GetTradeStatisticsBySymbolQueryHandler(
    ICexDbContext cexDbContext,
    ICurrentUser currentUser)
    : IRequestHandler<GetTradeStatisticsBySymbolQuery, TradeStatisticsDto>
{
    public async Task<TradeStatisticsDto> Handle(
        GetTradeStatisticsBySymbolQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        // Database-side aggregation: Group by Side and calculate sums
        var statistics = await cexDbContext.TradeHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Symbol == request.Symbol)
            .GroupBy(x => x.Side.ToLower())
            .Select(g => new
            {
                Side = g.Key,
                TotalFunds = g.Sum(x => x.Funds),
                TotalFee = g.Sum(x => x.Fee),
                TotalSize = g.Sum(x => x.Size)
            })
            .ToListAsync(cancellationToken);

        // Extract statistics for each side
        var buyStats = statistics.FirstOrDefault(x => x.Side == "buy");
        var sellStats = statistics.FirstOrDefault(x => x.Side == "sell");

        return new TradeStatisticsDto
        {
            Buy = ExtractSideStatistics(buyStats),
            Sell = ExtractSideStatistics(sellStats)
        };
    }

    /// <summary>
    /// Extracts and calculates statistics for a specific trading side (buy or sell).
    /// Encapsulates business logic for average price calculation.
    /// </summary>
    /// <param name="sideData">Aggregated data for a specific side, or null if no trades exist</param>
    /// <returns>Side statistics with calculated average price</returns>
    private static SideStatisticsDto ExtractSideStatistics(dynamic? sideData)
    {
        if (sideData is null)
        {
            return new SideStatisticsDto
            {
                TotalFunds = 0,
                TotalFee = 0,
                TotalSize = 0,
                AvgPrice = 0
            };
        }

        var totalFunds = (decimal)sideData.TotalFunds;
        var totalFee = (decimal)sideData.TotalFee;
        var totalSize = (decimal)sideData.TotalSize;

        return new SideStatisticsDto
        {
            TotalFunds = totalFunds,
            TotalFee = totalFee,
            TotalSize = totalSize,
            AvgPrice = CalculateAveragePrice(totalFunds, totalFee, totalSize)
        };
    }

    /// <summary>
    /// Calculates the weighted average price including fees.
    /// Business Rule: AvgPrice = (TotalFunds + TotalFee) / TotalSize
    /// </summary>
    /// <param name="totalFunds">Sum of all trade funds</param>
    /// <param name="totalFee">Sum of all trade fees</param>
    /// <param name="totalSize">Sum of all trade sizes</param>
    /// <returns>Average price, or 0 if total size is zero (division by zero guard)</returns>
    private static decimal CalculateAveragePrice(decimal totalFunds, decimal totalFee, decimal totalSize)
    {
        if (totalSize == 0)
        {
            return 0;
        }

        return (totalFunds + totalFee) / totalSize;
    }
}
