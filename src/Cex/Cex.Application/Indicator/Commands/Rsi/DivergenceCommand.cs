using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace Cex.Application.Indicator.Commands.Rsi
{
    public enum DivergenceType
    {
        None,
        Peak,
        Trough
    }


    public class DivergenceResult
    {
        public DivergenceResult(DivergenceType type)
        {
            Type = type;
        }

        public DivergenceResult(DivergenceType type, decimal entryPrice, DateTime time, decimal rsi)
        {
            Type = type;
            EntryPrice = entryPrice;
            Time = time;
            Rsi = rsi;
        }

        public DivergenceType Type { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime Time { get; set; }
        public decimal Rsi { get; set; }
    }

    public record DivergenceCommand(List<Kline> Candles) : IRequest<DivergenceResult>
    {
    }

    public class DivergenceCommandHandler(ISender sender) : IRequestHandler<DivergenceCommand, DivergenceResult>
    {
        public async Task<DivergenceResult> Handle(DivergenceCommand command, CancellationToken cancellationToken)
        {
            var candles = command.Candles;
            var lastCandle = candles[^3];
            var lastCandleOpenTime = lastCandle.OpenTime;

            var rsies = await sender.Send(new RsiCommand(candles), cancellationToken);

            var (peaks, troughs) = await sender.Send(new CalculateRsiPeaksAndTroughsCommand(rsies), cancellationToken);
            if (await sender.Send(new DivergenceRsiPeakCommand(lastCandleOpenTime, peaks, candles),
                    cancellationToken))
            {
                return new DivergenceResult(DivergenceType.Peak, candles[^2].ClosePrice, lastCandleOpenTime,
                    rsies[lastCandleOpenTime]);
            }

            if (await sender.Send(
                    new DivergenceRsiTroughCommand(lastCandleOpenTime, troughs, candles),
                    cancellationToken))
            {
                return new DivergenceResult(DivergenceType.Trough, candles[^2].OpenPrice, lastCandleOpenTime,
                    rsies[lastCandleOpenTime]);
            }

            return new DivergenceResult(DivergenceType.None);
        }
    }
}