using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.KuCoin.Commands.SyncTradeHistory
{
    public record SyncTradeHistoryCommand(DateTime StartAt) : IRequest;

    public class SyncTradeHistoryCommandHandler(
        IMapper mapper,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig) : IRequestHandler<SyncTradeHistoryCommand>
    {
        private readonly DateTime _startAt = new(2025, 9, 20);

        public async Task Handle(SyncTradeHistoryCommand command, CancellationToken cancellationToken)
        {
            var lastSyncDate = cexDbContext.TradeHistories
                .Where(x => x.Symbol == "XAUT-USDT")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.CreatedAt)
                .FirstOrDefault();
            var fromDate = _startAt > lastSyncDate ? _startAt : lastSyncDate;
            while (fromDate <= DateTime.UtcNow)
            {
                var tradeHis = await kuCoinService.GetTradeHistory("XAUT-USDT", fromDate, kuCoinConfig.Value);

                if (tradeHis.Items.Count > 0)
                {
                    var entities = mapper.Map<IList<TradeHistory>>(tradeHis.Items);

                    cexDbContext.TradeHistories.AddRange(entities);
                    await cexDbContext.SaveChangesAsync(cancellationToken);
                }

                fromDate = fromDate.AddDays(7);
            }
        }
    }
}