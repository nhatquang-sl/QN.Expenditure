using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signals.Commands.CheckSignalStopLoss;

public record CheckSignalStopLossCommand : IRequest;

public class CheckSignalStopLossCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalStopLossCommand>
{
    public async Task Handle(CheckSignalStopLossCommand command, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Signals
            .Where(s => s.EntryHitAt != null && s.StopLossHitAt == null)
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

                lastCandleOpenTime = batch[^1].OpenTime;
                startAt = lastCandleOpenTime.Value.AddMinutes(1);

                foreach (var signal in candidates.Where(s => s.StopLossHitAt == null))
                {
                    var hit = batch.FirstOrDefault(c =>
                        c.OpenTime > signal.LastCheckedCandleAt &&
                        (signal.SignalType == SignalType.Long
                            ? c.LowestPrice  <= signal.StopLoss
                            : c.HighestPrice >= signal.StopLoss));

                    if (hit is null) continue;

                    signal.StopLossHitAt = hit.OpenTime;
                    signal.StopLossHitAfterMinutes = (int)(hit.OpenTime - signal.DetectedAt).TotalMinutes;
                    signal.LastCheckedCandleAt = hit.OpenTime;
                    unhitCount--;
                }

                if (unhitCount == 0) break;
            }
        }
        catch (Exception ex)
        {
            // Any exception (rate-limit 429, network, timeout) during the loop:
            // fall through and save whatever progress was accumulated before the failure.
            logTrace.LogError("CheckSignalStopLoss loop interrupted — saving partial progress", ex);
        }

        if (lastCandleOpenTime is null) return;

        foreach (var signal in candidates.Where(s =>
            s.StopLossHitAt == null && lastCandleOpenTime.Value > s.LastCheckedCandleAt))
        {
            signal.LastCheckedCandleAt = lastCandleOpenTime.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
