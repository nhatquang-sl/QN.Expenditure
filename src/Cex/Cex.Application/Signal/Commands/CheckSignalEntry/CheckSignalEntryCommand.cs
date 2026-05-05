using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signal.Commands.CheckSignalEntry;

public record CheckSignalEntryCommand : IRequest;

public class CheckSignalEntryCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalEntryCommand>
{
    public async Task Handle(CheckSignalEntryCommand command, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.SignalRecords
            .Where(s => s.EntryHitAt == null)
            .OrderBy(s => s.LastCheckedCandleAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return;

        const IntervalType interval = IntervalType.OneMinute;
        var startAt = candidates.Min(s => s.LastCheckedCandleAt);
        var now = DateTime.UtcNow;
        DateTime? lastCandleOpenTime = null;
        var unhitCount = candidates.Count;

        try
        {
            while (startAt < now)
            {
                var batch = await kuCoinService.GetKlines(
                    "BTCUSDT", interval, startAt, interval.GetEndDate(startAt), kuCoinConfig.Value);

                if (batch.Count == 0) break;

                var batchHigh = batch.Max(c => c.HighestPrice);
                var batchLow  = batch.Min(c => c.LowestPrice);
                lastCandleOpenTime = batch[^1].OpenTime;
                startAt = lastCandleOpenTime.Value.AddMinutes(1);

                foreach (var signal in candidates.Where(s => s.EntryHitAt == null))
                {
                    var inRange = signal.SignalType == SignalType.Long
                        ? signal.EntryPrice >= batchLow
                        : signal.EntryPrice <= batchHigh;

                    if (!inRange) continue;

                    var checkFrom = signal.LastCheckedCandleAt;
                    var hit = batch
                        .FirstOrDefault(c => c.OpenTime > checkFrom &&
                                             (signal.SignalType == SignalType.Long
                                              ? c.LowestPrice  <= signal.EntryPrice
                                              : c.HighestPrice >= signal.EntryPrice));

                    if (hit is null) continue;

                    signal.EntryHitAt = hit.OpenTime;
                    signal.LastCheckedCandleAt = hit.OpenTime;  // anchor for stop-loss check
                    signal.MaxProfitCheckedAt  = hit.OpenTime;  // anchor for max-profit check
                    unhitCount--;
                }

                if (unhitCount == 0) break;
            }
        }
        catch (Exception ex)
        {
            logTrace.LogError("CheckSignalEntry loop interrupted — saving partial progress", ex);
        }

        if (lastCandleOpenTime is null) return;

        foreach (var signal in candidates.Where(s => s.EntryHitAt == null && lastCandleOpenTime.Value > s.LastCheckedCandleAt))
            signal.LastCheckedCandleAt = lastCandleOpenTime.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
