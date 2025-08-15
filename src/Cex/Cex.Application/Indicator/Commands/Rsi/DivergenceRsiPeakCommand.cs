using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands.Rsi
{
    public record DivergenceRsiPeakCommand(
        DateTime RsiTime,
        Dictionary<DateTime, decimal> Peaks,
        List<Kline> Candles) : IRequest<DateTime>
    {
    }

    public class DivergenceRsiPeakCommandHandler : IRequestHandler<DivergenceRsiPeakCommand, DateTime>
    {
        public Task<DateTime> Handle(DivergenceRsiPeakCommand command, CancellationToken cancellationToken)
        {
            var peaks = command.Peaks;
            var rsiCandle = command.Candles.First(c => c.OpenTime == command.RsiTime);
            var rsiIndex = command.Candles.IndexOf(rsiCandle);
            if (!peaks.TryGetValue(command.RsiTime, out var peak))
            {
                return Task.FromResult(default(DateTime));
            }

            var fromCandle = command.Candles[rsiIndex - 20];
            var previousPeaks = peaks
                .Where(p => p.Key < command.RsiTime && p.Key >= fromCandle.OpenTime)
                .ToDictionary(p => p.Key, p => p.Value);

            return Task.FromResult((from previousPeak in previousPeaks
                let previousPeakCandle = command.Candles.First(c => c.OpenTime == previousPeak.Key)
                where previousPeak.Value > peak && previousPeakCandle.HighestPrice  < rsiCandle.HighestPrice
                select previousPeak.Key).FirstOrDefault());
        }
    }
}