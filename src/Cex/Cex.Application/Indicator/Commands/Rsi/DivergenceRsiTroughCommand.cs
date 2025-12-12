using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands.Rsi
{
    public record DivergenceRsiTroughCommand(
        DateTime RsiTime,
        Dictionary<DateTime, decimal> Troughs,
        List<Kline> Candles)
        : IRequest<DateTime>
    {
    }


    public class DivergenceRsiTroughCommandHandler : IRequestHandler<DivergenceRsiTroughCommand, DateTime>
    {
        public Task<DateTime> Handle(DivergenceRsiTroughCommand command, CancellationToken cancellationToken)
        {
            var troughs = command.Troughs;
            var rsiCandle = command.Candles.First(c => c.OpenTime == command.RsiTime);
            var rsiIndex = command.Candles.IndexOf(rsiCandle);
            if (!troughs.TryGetValue(command.RsiTime, out var trough))
            {
                return Task.FromResult(default(DateTime));
            }

            var fromCandle = command.Candles[rsiIndex - 20];
            var previousTroughs = troughs
                .Where(p => p.Key < command.RsiTime && p.Key >= fromCandle.OpenTime)
                .ToDictionary(p => p.Key, p => p.Value);

            return Task.FromResult((from previousTrough in previousTroughs
                let previousTroughCandle = command.Candles.First(c => c.OpenTime == previousTrough.Key)
                where previousTrough.Value < trough && previousTroughCandle.LowestPrice > rsiCandle.LowestPrice
                select previousTrough.Key).FirstOrDefault());
        }
    }
}