using System.Text.Json;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
    public class RunIndicatorCommand : IRequest
    {
    }

    public class RunIndicatorCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender)
        : IRequestHandler<RunIndicatorCommand>
    {
        public async Task Handle(RunIndicatorCommand command, CancellationToken cancellationToken)
        {
            var candles = await kuCoinService.GetKlines("BTCUSDT", "5min", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow,
                kuCoinConfig.Value);
            var str = JsonSerializer.Serialize(candles);
            var lastCandle = candles[^2].OpenTime;

            var rises = await sender.Send(new RsiCommand(candles), cancellationToken);
            var rsi = rises[lastCandle];
            var bollingerBands = await sender.Send(new BollingerBandsCommand(candles), cancellationToken);
            var bollingerBand = bollingerBands[lastCandle];
        }
    }
}