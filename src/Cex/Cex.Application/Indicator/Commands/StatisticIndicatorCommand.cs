using Cex.Application.Indicator.Commands.Rsi;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
    public class StatisticIndicatorCommand : IRequest
    {
    }

    public class StatisticIndicatorCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender,
        INotifier notifier)
        : IRequestHandler<StatisticIndicatorCommand>
    {
        public async Task Handle(StatisticIndicatorCommand command, CancellationToken cancellationToken)
        {
            var candles = await kuCoinService.GetKlines("BTCUSDT", "15min",
                DateTime.UtcNow.AddDays(-15), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);

            var rsies = await sender.Send(new RsiCommand(candles), cancellationToken);
            var bollingerBands = await sender.Send(new BollingerBandsCommand(candles), cancellationToken);

            var (peaks, troughs) = await sender.Send(new CalculateRsiPeaksAndTroughsCommand(rsies), cancellationToken);

            var peakKeys = peaks.Keys.ToList();

            for (var i = 10; i < peakKeys.Count; i++)
            {
                var lastCandleOpenTime = peakKeys[i];
                var lastCandle = candles.First(c => c.OpenTime == lastCandleOpenTime);
                var lastCandleIndex = candles.IndexOf(lastCandle);
                var fromCandle = candles[lastCandleIndex - 20];
                var lastRsi = rsies[lastCandleOpenTime];

                var previousPeaks = peaks
                    .Where(p => p.Key < lastCandleOpenTime && p.Key >= fromCandle.OpenTime)
                    .ToDictionary(p => p.Key, p => p.Value);
                var leverage = 10;
                foreach (var previousPeak in previousPeaks)
                {
                    var peakCandle = candles.First(c => c.OpenTime == previousPeak.Key);
                    if (previousPeak.Value <= lastRsi || peakCandle.HighestPrice >= lastCandle.HighestPrice)
                    {
                        continue;
                    }

                    var entryCandle = candles[lastCandleIndex + 1];
                    var entryPrice = entryCandle.ClosePrice;
                    var liquidationPrice = (entryPrice + 0.7m * entryPrice / leverage).FixedNumber();
                    var stoploss = 0m;
                    var profit = 0m;

                    for (var j = lastCandleIndex + 2; j < candles.Count; j++)
                    {
                        var currentCandle = candles[j];
                        var bb = bollingerBands[currentCandle.OpenTime];
                        if (currentCandle.HighestPrice > liquidationPrice)
                        {
                            break;
                        }

                        if (stoploss != 0 && currentCandle.HighestPrice > stoploss)
                        {
                            profit = (leverage * 100 * (entryPrice - stoploss) / entryPrice).FixedNumber();
                            break;
                        }


                        if (stoploss == 0 && currentCandle.LowestPrice < bb.Sma20)
                        {
                            stoploss = bb.Sma20;
                        }

                        if (stoploss > bb.Bold && currentCandle.LowestPrice < bb.Bold)
                        {
                            stoploss = bb.Bold;
                        }
                    }
                    // var msg = new StringBuilder();
                    // msg.AppendLine($"<pre>Divergence peak RSI detected at <i>[{lastCandleOpenTime.ToSimple()}]</i>");
                    // msg.AppendLine($" <i>[{lastRsi}]</i>: <b>{lastCandle.HighestPrice}</b>");
                    // msg.AppendLine($"Compare with peak at <i>[{previousPeak.Key.ToSimple()}]</i>:");
                    // msg.AppendLine($" <i>[{previousPeak.Value}]</i>: <b>{peakCandle.HighestPrice}</b></pre>");
                    // await notifier.Notify(msg.ToString(), cancellationToken);

                    break;
                }
            }
        }
    }
}