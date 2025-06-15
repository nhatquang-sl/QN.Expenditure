using System.ComponentModel;
using System.Text;
using Cex.Application.Indicator.Commands.Rsi;
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

    public static class IntervalTypeExtensions
    {
        public static TimeSpan GetTimeSpan(this IntervalType intervalType)
        {
            return intervalType switch
            {
                IntervalType.FiveMinutes => TimeSpan.FromMinutes(5),
                IntervalType.FifteenMinutes => TimeSpan.FromMinutes(15),
                IntervalType.ThirtyMinutes => TimeSpan.FromMinutes(30),
                IntervalType.OneHour => TimeSpan.FromHours(1),
                IntervalType.FourHours => TimeSpan.FromHours(4),
                IntervalType.OneDay => TimeSpan.FromDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(intervalType),
                    $"Unsupported interval type: {intervalType}")
            };
        }

        public static DateTime GetStartDate(this IntervalType intervalType)
        {
            return intervalType switch
            {
                IntervalType.FiveMinutes => DateTime.UtcNow.AddDays(-5),
                IntervalType.FifteenMinutes => DateTime.UtcNow.AddDays(-15),
                IntervalType.ThirtyMinutes => DateTime.UtcNow.AddDays(-30),
                IntervalType.OneHour => DateTime.UtcNow.AddDays(-60),
                IntervalType.FourHours => DateTime.UtcNow.AddDays(-120),
                IntervalType.OneDay => DateTime.UtcNow.AddDays(-1440),
                _ => throw new ArgumentOutOfRangeException(nameof(intervalType),
                    $"Unsupported interval type: {intervalType}")
            };
        }
    }

    public class RunIndicatorCommand(IntervalType interval) : IRequest
    {
        public IntervalType Type { get; set; } = interval;
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
                command.Type.GetStartDate(), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);

            var lastCandle = candles[^3];
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