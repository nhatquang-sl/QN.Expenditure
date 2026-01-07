using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Trade.Queries.GetTradeHistoriesBySymbol;

public class GetTradeHistoriesBySymbolQueryHandler(
    ICexDbContext cexDbContext,
    ICurrentUser currentUser)
    : IRequestHandler<GetTradeHistoriesBySymbolQuery, PaginatedList<TradeHistoryDto>>
{
    public async Task<PaginatedList<TradeHistoryDto>> Handle(
        GetTradeHistoriesBySymbolQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        // Build base query with filters
        var query = cexDbContext.TradeHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Symbol == request.Symbol)
            .OrderByDescending(x => x.TradedAt);

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new TradeHistoryDto
            {
                Symbol = x.Symbol,
                TradeId = x.TradeId,
                OrderId = x.OrderId,
                Side = x.Side,
                Price = x.Price,
                Size = x.Size,
                Funds = x.Funds,
                Fee = x.Fee,
                FeeCurrency = x.FeeCurrency,
                TradedAt = x.TradedAt
            })
            .ToListAsync(cancellationToken);

        // Calculate total pages
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PaginatedList<TradeHistoryDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }
}
