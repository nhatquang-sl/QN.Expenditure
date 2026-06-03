using Cex.Application.Signals.Queries.GetSignals;
using Cex.Domain.Enums;
using Lib.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/signals")]
[ApiController]
public class SignalsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Retrieves paginated signals filtered by date range, interval, and signal type.
    /// </summary>
    /// <param name="from">Start of the date range (required, inclusive)</param>
    /// <param name="to">End of the date range (required, inclusive)</param>
    /// <param name="interval">Optional interval filter (e.g., 1min, 5min, 15min, 30min, 1hour, 4hour, 1day)</param>
    /// <param name="signalType">Optional signal type filter (Long or Short)</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="sortBy">Sort field: createdAt (default), entryHitAfterMinutes, maxProfitHitAfterMinutes, stopLossHitAfterMinutes</param>
    /// <param name="sortOrder">Sort direction: asc or desc (default: desc)</param>
    /// <returns>Paginated list of signals with entryHitAfterMinutes, maxProfitHitAfterMinutes, and stopLossHitAfterMinutes persisted fields</returns>
    [HttpGet]
    public Task<PaginatedList<SignalDto>> GetSignals(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? interval = null,
        [FromQuery] SignalType? signalType = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
        => sender.Send(new GetSignalsQuery(from, to, interval, signalType, pageNumber, pageSize, sortBy, sortOrder));
}
