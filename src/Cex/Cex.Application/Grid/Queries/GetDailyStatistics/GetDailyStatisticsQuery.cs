using Cex.Application.Common.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Queries.GetDailyStatistics
{
    public record GetDailyStatisticsQuery(DateTime DateTime) : IRequest<List<UserProfit>>
    {
    }

    public class UserProfit
    {
        public long UserId { get; init; }
        public decimal Profit { get; init; }
    }

    public class GetDailyStatisticsQueryHandler(ICexDbContext cexDbContext)
        : IRequestHandler<GetDailyStatisticsQuery, List<UserProfit>>
    {
        public async Task<List<UserProfit>> Handle(GetDailyStatisticsQuery command, CancellationToken cancellationToken)
        {
            var orders = await cexDbContext.SpotOrders
                .Where(o => o.CreatedAt.Date == command.DateTime.Date)
                .ToListAsync(cancellationToken);

            var profitByUser = orders
                .GroupBy(o => o.UserId)
                .Select(g => new UserProfit
                {
                    UserId = long.Parse(g.Key), // Convert g.Key to long
                    Profit = g.Sum(o =>
                        string.Equals(o.Side, "SELL", StringComparison.OrdinalIgnoreCase)
                            ? o.Price * o.OrigQty - o.Fee
                            : string.Equals(o.Side, "BUY", StringComparison.OrdinalIgnoreCase)
                                ? -(o.Price * o.OrigQty + o.Fee)
                                : 0)
                })
                .ToList();

            return profitByUser ?? [];
        }
    }
}