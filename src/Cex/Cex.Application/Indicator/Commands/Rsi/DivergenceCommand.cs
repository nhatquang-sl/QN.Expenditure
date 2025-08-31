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

        public DivergenceResult(DivergenceType type, decimal entryPrice, DateTime time, decimal rsi,
            DateTime previousTime)
        {
            Type = type;
            EntryPrice = entryPrice;
            Time = time;
            Rsi = rsi;
            PreviousTime = previousTime;
        }

        public DivergenceType Type { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime Time { get; set; }
        
        public decimal Rsi { get; set; }
        public DateTime PreviousTime { get; set; }
    }

    public record DivergenceCommand(List<Kline> Candles, Dictionary<DateTime, decimal> RsiValues)
        : IRequest<DivergenceResult>
    {
    }

    public class DivergenceCommandHandler(ISender sender) : IRequestHandler<DivergenceCommand, DivergenceResult>
    {
        public async Task<DivergenceResult> Handle(DivergenceCommand command, CancellationToken cancellationToken)
        {
            var candles = command.Candles;
            var rsiValues = command.RsiValues;
            var lastCandle = candles[^3];
            var lastCandleOpenTime = lastCandle.OpenTime;

            var (peaks, troughs) =
                await sender.Send(new CalculateRsiPeaksAndTroughsCommand(rsiValues), cancellationToken);

            var previousPeakTime = await sender.Send(new DivergenceRsiPeakCommand(lastCandleOpenTime, peaks, candles),
                cancellationToken);
            if (previousPeakTime != default)
            {
                return new DivergenceResult(DivergenceType.Peak, candles[^2].ClosePrice, lastCandleOpenTime,
                    rsiValues[lastCandleOpenTime], previousPeakTime);
            }

            var previousTroughTime =
                await sender.Send(new DivergenceRsiTroughCommand(lastCandleOpenTime, troughs, candles),
                    cancellationToken);
            if (previousTroughTime != default)
            {
                return new DivergenceResult(DivergenceType.Trough, candles[^2].OpenPrice, lastCandleOpenTime,
                    rsiValues[lastCandleOpenTime], previousTroughTime);
            }

            return new DivergenceResult(DivergenceType.None);
        }
    }
}