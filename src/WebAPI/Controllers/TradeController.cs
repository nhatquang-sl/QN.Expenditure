using Cex.Application.Trade.Commands.SyncTradeHistoryBySymbol;
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
}
