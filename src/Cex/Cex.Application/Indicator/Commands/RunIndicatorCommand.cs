using System.Text;
using Cex.Application.Indicator.Commands.Rsi;
using Cex.Application.Indicator.Shared;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
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
            var candles = await kuCoinService.GetKlines("BTCUSDT", command.Type.GetDescription(),
                command.Type.GetStartDate(), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);

            var divergence = await sender.Send(new DivergenceCommand(candles), cancellationToken);
            switch (divergence.Type)
            {
                case DivergenceType.Peak:
                {
                    var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Short</b> detected:\n");
                    msg.AppendLine($" [{divergence.Time.ToSimple()}]: <b>{divergence.Rsi}</b>");
                    msg.AppendLine($" Entry price: <b>{candles[^1].ClosePrice}</b>");
                    await notifier.Notify(msg.ToString(), cancellationToken);
                    break;
                }
                case DivergenceType.Trough:
                {
                    var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Long</b> detected:\n");
                    msg.AppendLine($" [{divergence.Time.ToSimple()}]: <b>{divergence.Rsi}</b>");
                    msg.AppendLine($" Entry price: <b>{candles[^1].OpenPrice}</b>");
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