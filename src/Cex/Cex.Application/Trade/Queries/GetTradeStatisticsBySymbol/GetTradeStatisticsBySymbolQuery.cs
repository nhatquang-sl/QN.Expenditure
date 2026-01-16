using MediatR;

namespace Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;

public record GetTradeStatisticsBySymbolQuery(
    string Symbol
) : IRequest<TradeStatisticsDto>;
