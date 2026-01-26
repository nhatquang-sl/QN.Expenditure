using System.Text;
using Cex.Application.Indicator.Commands.Rsi;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
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
            var candles = await kuCoinService.GetKlines("BTCUSDT", command.Type,
                command.Type.GetStartDate(), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);
            var rsiValues = await sender.Send(new RsiCommand(candles), cancellationToken);
            var div = await sender.Send(new DivergenceCommand(candles, rsiValues), cancellationToken);
            var divTime = div.Time.ToSimple();
            var divPreTime = div.PreviousTime.ToSimple();
            switch (div.Type)
            {
                case DivergenceType.Peak:
                {
                    var dCandle = candles.First(x => x.OpenTime == div.Time);
                    var preCandle = candles.First(x => x.OpenTime == div.PreviousTime);
                    var entryPrice = candles[^1].ClosePrice;

                    var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Short</b> detected:\n");
                    msg.AppendLine($"[{divTime}]: <b>{div.Rsi} - {dCandle.HighestPrice}</b>");
                    msg.AppendLine($"[{divPreTime}]: <b>{rsiValues[div.PreviousTime]} - {preCandle.HighestPrice}</b>");
                    msg.AppendLine($"Entry price: <b>{entryPrice}</b>");
                    msg.AppendLine($"Liquidation 8x10: <b>{(entryPrice * 1.08m).FixedNumber(2)}</b>");
                    await notifier.Notify(msg.ToString(), cancellationToken);
                    break;
                }
                case DivergenceType.Trough:
                {
                    var dCandle = candles.First(x => x.OpenTime == div.Time);
                    var preCandle = candles.First(x => x.OpenTime == div.PreviousTime);
                    var entryPrice = candles[^1].OpenPrice;

                    var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Long</b> detected:\n");
                    msg.AppendLine($"[{divTime}]: <b>{div.Rsi} - {dCandle.LowestPrice}</b>");
                    msg.AppendLine($"[{divPreTime}]: <b>{rsiValues[div.PreviousTime]} - {preCandle.LowestPrice}</b>");
                    msg.AppendLine($"Entry price: <b>{entryPrice}</b>");
                    msg.AppendLine($"Liquidation 8x10: <b>{(entryPrice * 0.92m).FixedNumber(2)}</b>");
                    await notifier.Notify(msg.ToString(), cancellationToken);
                    break;
                }
                case DivergenceType.None:
                default:
                    return;
            }
        }
    }
}