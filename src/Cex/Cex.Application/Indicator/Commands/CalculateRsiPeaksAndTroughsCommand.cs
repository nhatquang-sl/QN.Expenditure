using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
    public record CalculateRsiPeaksAndTroughsCommand(Dictionary<DateTime, decimal> Rsies)
        : IRequest<(Dictionary<DateTime, decimal>, Dictionary<DateTime, decimal>)>
    {
    }

    public class CalculateRsiPeaksAndTroughsCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender,
        INotifier notifier)
        : IRequestHandler<CalculateRsiPeaksAndTroughsCommand, (Dictionary<DateTime, decimal>,
            Dictionary<DateTime, decimal>)>
    {
        public Task<(Dictionary<DateTime, decimal>, Dictionary<DateTime, decimal>)> Handle(
            CalculateRsiPeaksAndTroughsCommand command, CancellationToken cancellationToken)
        {
            var peaks = new Dictionary<DateTime, decimal>();
            var troughs = new Dictionary<DateTime, decimal>();
            var keys = command.Rsies.Keys.ToList();

            for (var i = 1; i < keys.Count - 1; i++)
            {
                var key = keys[i];
                var previousValue = command.Rsies[keys[i - 1]];
                var currentValue = command.Rsies[keys[i]];
                var nextValue = command.Rsies[keys[i + 1]];

                // Check if the current value is a peak
                if (currentValue > previousValue && currentValue > nextValue && currentValue > 68)
                {
                    peaks[keys[i]] = currentValue;
                }

                // Check if the current value is a trough
                if (currentValue < previousValue && currentValue < nextValue && currentValue < 32)
                {
                    troughs[keys[i]] = currentValue;
                }
            }

            return Task.FromResult((peaks, troughs));
        }
    }
}