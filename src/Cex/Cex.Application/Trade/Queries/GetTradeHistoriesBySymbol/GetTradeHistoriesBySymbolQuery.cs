using Lib.Application.Models;
using MediatR;

namespace Cex.Application.Trade.Queries.GetTradeHistoriesBySymbol;

public record GetTradeHistoriesBySymbolQuery(
    string Symbol,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PaginatedList<TradeHistoryDto>>;
