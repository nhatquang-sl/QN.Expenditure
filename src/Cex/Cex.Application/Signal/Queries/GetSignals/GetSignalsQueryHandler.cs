using Cex.Application.Common.Abstractions;
using Lib.Application.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Signals.Queries.GetSignals;

public class GetSignalsQueryHandler(ICexDbContext dbContext)
    : IRequestHandler<GetSignalsQuery, PaginatedList<SignalDto>>
{
    public async Task<PaginatedList<SignalDto>> Handle(
        GetSignalsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Signals
            .AsNoTracking()
            .Where(s => s.DetectedAt >= request.From && s.DetectedAt <= request.To);

        if (!string.IsNullOrEmpty(request.Interval))
            query = query.Where(s => s.Interval == request.Interval);

        if (request.SignalType.HasValue)
            query = query.Where(s => s.SignalType == request.SignalType.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.DetectedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SignalDto
            {
                Id = s.Id,
                Symbol = s.Symbol,
                Interval = s.Interval,
                SignalType = s.SignalType.ToString(),
                DetectedAt = s.DetectedAt,
                RsiValue = s.RsiValue,
                PreviousRsiValue = s.PreviousRsiValue,
                EntryPrice = s.EntryPrice,
                StopLoss = s.StopLoss,
                TakeProfit = s.TakeProfit,
                Leverage = s.Leverage,
                MaxProfit = s.MaxProfit,
                MaxProfitHitAt = s.MaxProfitHitAt,
                EntryHitAt = s.EntryHitAt,
                StopLossHitAt = s.StopLossHitAt,
                TakeProfitHitAt = s.TakeProfitHitAt,
                CreatedAt = s.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PaginatedList<SignalDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            TotalPages = totalPages,
            TotalCount = totalCount,
        };
    }
}
