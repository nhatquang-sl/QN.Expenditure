using System.ComponentModel;
using System.Text;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
    public enum IntervalType
    {
        [Description("5min")] FiveMinutes,
        [Description("15min")] FifteenMinutes,
        [Description("30min")] ThirtyMinutes,
        [Description("1hour")] OneHour,
        [Description("4hour")] FourHours,
        [Description("1day")] OneDay
    }

    public class RunIndicatorCommand(IntervalType interval) : IRequest
    {
        public IntervalType Type { get; set; } = interval;

        public DateTime GetStartDate()
        {
            var minutesOffset = Type switch
            {
                IntervalType.FiveMinutes => -5,
                IntervalType.FifteenMinutes => -15,
                IntervalType.ThirtyMinutes => -30,
                IntervalType.OneHour => -60,
                IntervalType.FourHours => -120,
                IntervalType.OneDay => -1440,
                _ => throw new ArgumentOutOfRangeException(nameof(Type), $"Unsupported interval type: {Type}")
            };

            return DateTime.UtcNow.AddDays(minutesOffset);
        }
    }

    public class RunIndicatorCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender,
        INotifier notifier,
        ILogTrace logTrace)
        : IRequestHandler<RunIndicatorCommand>
    {
        public async Task Handle(RunIndicatorCommand command, CancellationToken cancellationToken)
        {
            logTrace.LogInformation(command.Type.GetDescription());
            var candles = await kuCoinService.GetKlines("BTCUSDT", command.Type.GetDescription(),
                command.GetStartDate(), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);

            var lastCandle = candles[^2];
            var lastCandleOpenTime = lastCandle.OpenTime;

            var rsies = await sender.Send(new RsiCommand(candles), cancellationToken);
            var bollingerBands = await sender.Send(new BollingerBandsCommand(candles), cancellationToken);

            var (peaks, troughs) = await sender.Send(new CalculateRsiPeaksAndTroughsCommand(rsies), cancellationToken);
            var isDivergencePeak = await sender.Send(new DivergenceRsiPeakCommand(lastCandleOpenTime, peaks, candles),
                cancellationToken);
            var isDivergenceTrough = await sender.Send(
                new DivergenceRsiTroughCommand(lastCandleOpenTime, troughs, candles),
                cancellationToken);
            if (isDivergencePeak)
            {
                var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Short</b> detected:\n");
                msg.AppendLine($" [{lastCandleOpenTime.ToSimple()}]: <b>{rsies[lastCandleOpenTime]}</b>");
                msg.AppendLine($" Entry price: <b>{candles[^1].ClosePrice}</b>");
                await notifier.Notify(msg.ToString(), cancellationToken);
            }

            if (isDivergenceTrough)
            {
                var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Long</b> detected:\n");
                msg.AppendLine($" [{lastCandleOpenTime.ToSimple()}]: <b>{rsies[lastCandleOpenTime]}</b>");
                msg.AppendLine($" Entry price: <b>{candles[^1].OpenPrice}</b>");
                await notifier.Notify(msg.ToString(), cancellationToken);
            }
        }
    }
}