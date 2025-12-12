using Cex.Application.Trade.Commands.SyncTradeHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/trade/sync")]
[ApiController]
public class SyncTradeHistoryController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Triggers synchronization of trade history from exchanges to local database.
    /// Processes all configured exchange settings and sync settings in batches.
    /// </summary>
    [HttpPost]
    public Task SyncTradeHistory()
        => sender.Send(new SyncTradeHistoryCommand());
}
