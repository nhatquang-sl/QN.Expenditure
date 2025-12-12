using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands
{
    public record BollingerBandsCommand(List<Kline> Candles) : IRequest<Dictionary<DateTime, BollingerBands>>
    {
    }

    public record BollingerBands(decimal Sma20, decimal Bolu, decimal Bold)
    {
    }


    public class
        BollingerBandsCommandHandler : IRequestHandler<BollingerBandsCommand, Dictionary<DateTime, BollingerBands>>
    {
        public Task<Dictionary<DateTime, BollingerBands>> Handle(BollingerBandsCommand command,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<DateTime, BollingerBands>();
            var candles = command.Candles;
            const int period = 20;
            const int multiplier = 2;
            for (var i = period - 1; i < candles.Count; i++)
            {
                // Take the window of 'period' klines ending at index i.
                var window = candles.Skip(i - period + 1).Take(period).ToList();
                var sma20 = (window.Sum(k => k.ClosePrice) / period).FixedNumber(1);

                // Calculate the population variance and then the standard deviation.
                var variance = window.Sum(k => (k.ClosePrice - sma20) * (k.ClosePrice - sma20)) / period;
                var stdDev = (decimal)Math.Sqrt((double)variance);

                var bolu = (sma20 + multiplier * stdDev).FixedNumber(1);
                var bold = (sma20 - multiplier * stdDev).FixedNumber(1);

                result.Add(candles[i].OpenTime, new BollingerBands(sma20, bolu, bold));
            }

            return Task.FromResult(result);
        }
    }
}