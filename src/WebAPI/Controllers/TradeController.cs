using Cex.Application.Trade.Commands.SyncTradeHistoryBySymbol;
using Cex.Application.Trade.Queries.GetTradeHistoriesBySymbol;
using Cex.Application.Trade.Queries.GetTradeStatisticsBySymbol;
using Lib.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/trade")]
[ApiController]
public class TradeController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Triggers synchronization of trade history for a specific symbol.
    /// Returns statistics including total synced, buy/sell amounts, and profit.
    /// </summary>
    /// <param name="symbol">The symbol to sync</param>
    /// <returns>Sync result with trade statistics</returns>
    [HttpPost("sync/{symbol}")]
    public Task<SyncTradeHistoryBySymbolResult> SyncTradeHistoryBySymbol(string symbol)
        => sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

    /// <summary>
    /// Retrieves paginated trade history for a specific symbol.
    /// Returns trades sorted by most recent first.
    /// </summary>
    /// <param name="symbol">The cryptocurrency symbol (e.g., BTC-USDT)</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <returns>Paginated list of trade history records</returns>
    [HttpGet("history/{symbol}")]
    public Task<PaginatedList<TradeHistoryDto>> GetTradeHistoriesBySymbol(
        string symbol,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => sender.Send(new GetTradeHistoriesBySymbolQuery(symbol, pageNumber, pageSize));

    /// <summary>
    /// Retrieves aggregated trade statistics for a specific symbol.
    /// Returns separate buy and sell statistics including total funds, fees, sizes, and average prices.
    /// </summary>
    /// <param name="symbol">The cryptocurrency symbol (e.g., BTC-USDT)</param>
    /// <returns>Trade statistics with buy and sell side data</returns>
    [HttpGet("statistics/{symbol}")]
    public Task<TradeStatisticsDto> GetTradeStatisticsBySymbol(string symbol)
        => sender.Send(new GetTradeStatisticsBySymbolQuery(symbol));
}
