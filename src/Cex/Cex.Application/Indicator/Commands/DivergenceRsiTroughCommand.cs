using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands
{
    public record DivergenceRsiTroughCommand(
        DateTime RsiTime,
        Dictionary<DateTime, decimal> Troughs,
        List<Kline> Candles)
        : IRequest<bool>
    {
    }


    public class DivergenceRsiTroughCommandHandler : IRequestHandler<DivergenceRsiTroughCommand, bool>
    {
        public Task<bool> Handle(DivergenceRsiTroughCommand command, CancellationToken cancellationToken)
        {
            var troughs = command.Troughs;
            var rsiCandle = command.Candles.First(c => c.OpenTime == command.RsiTime);
            var rsiIndex = command.Candles.IndexOf(rsiCandle);
            if (!troughs.TryGetValue(command.RsiTime, out var trough))
            {
                return Task.FromResult(false);
            }

            var fromCandle = command.Candles[rsiIndex - 20];
            var previousTroughs = troughs
                .Where(p => p.Key < command.RsiTime && p.Key >= fromCandle.OpenTime)
                .ToDictionary(p => p.Key, p => p.Value);

            return Task.FromResult((from previousTrough in previousTroughs
                let previousTroughCandle = command.Candles.First(c => c.OpenTime == previousTrough.Key)
                where previousTrough.Value < trough && previousTroughCandle.LowestPrice > rsiCandle.LowestPrice
                select previousTrough).Any());
        }
    }
}