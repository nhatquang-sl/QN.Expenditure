using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands.Rsi
{
    public record RsiCommand(List<Kline> Candles) : IRequest<Dictionary<DateTime, decimal>>
    {
    }

    public class RsiCommandHandler : IRequestHandler<RsiCommand, Dictionary<DateTime, decimal>>
    {
        public Task<Dictionary<DateTime, decimal>> Handle(RsiCommand command,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<DateTime, decimal>();
            var candles = command.Candles;
            const int period = 14;
            decimal avgGain = -1,
                avgLoss = -1;
            var rsiKlines = new List<RsiKline>();

            for (var i = 1; i < candles.Count; i++)
            {
                var kline = candles[i];
                var change = candles[i].ClosePrice - candles[i - 1].ClosePrice;
                var gain = change > 0 ? change : 0;
                var loss = change < 0 ? Math.Abs(change) : 0;
                rsiKlines.Add(new RsiKline
                {
                    Gain = gain,
                    Loss = loss
                });

                if (i <= 20)
                {
                    continue;
                }

                var (rsi, newAvgGain, newAvgLoss) = RelativeStrengthIndex(
                    rsiKlines.Skip(i - period).Take(period).ToList(),
                    avgGain, avgLoss);
                avgGain = newAvgGain;
                avgLoss = newAvgLoss;
                rsiKlines.Last().Rsi = rsi;

                result.Add(kline.OpenTime, rsi);
            }

            return Task.FromResult(result);
        }

        private static (decimal, decimal, decimal) RelativeStrengthIndex(List<RsiKline> klines, decimal avgGain,
            decimal avgLoss)
        {
            var period = klines.Count;
            if (avgGain == -1 && avgLoss == -1)
            {
                avgGain = klines.Sum(x => x.Gain) / period;
                avgLoss = klines.Sum(x => x.Loss) / period;
            }
            else
            {
                avgGain = (avgGain * (period - 1) + klines.Last().Gain) / period;
                avgLoss = (avgLoss * (period - 1) + klines.Last().Loss) / period;
            }

            var rs = avgGain / avgLoss;
            var rsi = (100 - 100 / (1 + rs)).FixedNumber(2);
            return (rsi, avgGain, avgLoss);
        }
    }

    internal class RsiKline
    {
        public decimal Gain { get; init; }
        public decimal Loss { get; init; }
        public decimal Rsi { get; set; }
    }
}