using Cex.Application.Common.Abstractions;
using Cex.Application.Common.ExServices.Cex;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Candle.Commands.SyncCandles
{
    public record SyncCandlesCommand(string AccessToken) : IRequest { }

    public class SyncCandlesCommandHandler(ICexDbContext cexDbContext, ICexService cexService)
    : IRequestHandler<SyncCandlesCommand>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICexService _cexService = cexService;

        public async Task Handle(SyncCandlesCommand command, CancellationToken cancellationToken)
        {
            var cexPrices = await _cexService.GetPrices(command.AccessToken);
            var minSession = cexPrices.Min(x => x.Session);
            var rmCandles = await _cexDbContext.Candles.Where(x => x.Session >= minSession).ToListAsync(cancellationToken);
            _cexDbContext.Candles.RemoveRange(rmCandles);

            _cexDbContext.Candles.AddRange(cexPrices);

            await _cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
