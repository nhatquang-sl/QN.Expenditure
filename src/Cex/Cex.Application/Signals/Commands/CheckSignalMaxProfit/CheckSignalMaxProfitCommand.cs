using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signals.Commands.CheckSignalMaxProfit;

public record CheckSignalMaxProfitCommand : IRequest;

public class CheckSignalMaxProfitCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalMaxProfitCommand>
{
    public async Task Handle(CheckSignalMaxProfitCommand command, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Signals
            .Where(s => s.EntryHitAt != null &&
                        (s.StopLossHitAt == null ||
                         (s.MaxProfitCheckedAt ?? s.EntryHitAt) < s.StopLossHitAt))
            .OrderBy(s => s.MaxProfitCheckedAt ?? s.EntryHitAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        logTrace.LogInformation("Total signals", candidates.Count);
        if (candidates.Count == 0) return;

        const IntervalType interval = IntervalType.OneMinute;
        var startAt = candidates.Min(s => s.MaxProfitCheckedAt ?? s.EntryHitAt!.Value);
        var now = DateTime.UtcNow;
        DateTime? lastBatchOpenTime = null;

        try
        {
            while (startAt < now)
            {
                var batch = await kuCoinService.GetKlines(
                    "BTCUSDT", interval, startAt, interval.GetEndDate(startAt), kuCoinConfig.Value);

                if (batch.Count == 0) break;

                lastBatchOpenTime = batch[^1].OpenTime;
                startAt = lastBatchOpenTime.Value.AddMinutes(1);

                foreach (var signal in candidates)
                {
                    var scanEnd = signal.StopLossHitAt;

                    // GetKlines returns candles sorted ascending by OpenTime.
                    // relevantCandles inherits that order; no defensive sort is applied.
                    var relevantCandles = batch
                        .Where(c => c.OpenTime > (signal.MaxProfitCheckedAt ?? signal.EntryHitAt!.Value) &&
                                    (scanEnd == null || c.OpenTime <= scanEnd))
                        .ToList();

                    if (relevantCandles.Count == 0) continue;

                    foreach (var c in relevantCandles)
                    {
                        // signal.Leverage is int; multiplying decimal by int promotes int to decimal — no truncation.
                        var profitPct = signal.SignalType == SignalType.Long
                            ? (c.HighestPrice - signal.EntryPrice) / signal.EntryPrice * 100m * signal.Leverage
                            : (signal.EntryPrice - c.LowestPrice) / signal.EntryPrice * 100m * signal.Leverage;

                        if (profitPct > signal.MaxProfit)
                        {
                            signal.MaxProfit = profitPct;
                            signal.MaxProfitHitAt = c.OpenTime;
                        }
                    }

                    // relevantCandles is sorted ascending; [^1] is the latest candle in the range.
                    // Its OpenTime is always ≤ scanEnd because of the Where filter above.
                    var newPointer = relevantCandles[^1].OpenTime;
                    if (newPointer > (signal.MaxProfitCheckedAt ?? signal.EntryHitAt!.Value))
                        signal.MaxProfitCheckedAt = newPointer;
                }
            }
        }
        catch (Exception ex)
        {
            // Any exception (rate-limit 429, network, timeout) during the loop:
            // fall through and save whatever progress was accumulated before the failure.
            logTrace.LogError("CheckSignalMaxProfit loop interrupted — saving partial progress", ex);
        }

        if (lastBatchOpenTime is null) return;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
