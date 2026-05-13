using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Extensions;
using Lib.Application.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Signals.Queries.GetSignals;

public record GetSignalsQuery(
    DateTime From,
    DateTime To,
    string? Interval = null,
    SignalType? SignalType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PaginatedList<SignalDto>>;

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

        var raw = await query
            .OrderByDescending(s => s.DetectedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Symbol,
                s.Interval,
                s.SignalType,
                s.DetectedAt,
                s.RsiValue,
                s.PreviousRsiValue,
                s.EntryPrice,
                s.StopLoss,
                s.TakeProfit,
                s.Leverage,
                s.MaxProfit,
                s.MaxProfitHitAt,
                s.EntryHitAt,
                s.StopLossHitAt,
                s.TakeProfitHitAt,
                s.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(s => new SignalDto
        {
            Id = s.Id,
            Symbol = s.Symbol,
            Interval = s.Interval,
            SignalType = s.SignalType.ToString(),
            DetectedAt = s.DetectedAt.ToUnixTimestampMilliseconds(),
            RsiValue = s.RsiValue,
            PreviousRsiValue = s.PreviousRsiValue,
            EntryPrice = s.EntryPrice,
            StopLoss = s.StopLoss,
            TakeProfit = s.TakeProfit,
            Leverage = s.Leverage,
            MaxProfit = s.MaxProfit,
            MaxProfitHitAt = s.MaxProfitHitAt?.ToUnixTimestampMilliseconds(),
            EntryHitAt = s.EntryHitAt?.ToUnixTimestampMilliseconds(),
            StopLossHitAt = s.StopLossHitAt?.ToUnixTimestampMilliseconds(),
            TakeProfitHitAt = s.TakeProfitHitAt?.ToUnixTimestampMilliseconds(),
            CreatedAt = s.CreatedAt.ToUnixTimestampMilliseconds(),
        }).ToList();

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
