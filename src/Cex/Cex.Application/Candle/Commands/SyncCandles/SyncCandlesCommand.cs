using AutoMapper;
using Cex.Application.Common.Abstractions;
using Lib.ExternalServices.Cex;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Candle.Commands.SyncCandles
{
    public record SyncCandlesCommand(string AccessToken) : IRequest { }

    public class SyncCandlesCommandHandler(ICexDbContext cexDbContext, ICexService cexService, IMapper mapper)
    : IRequestHandler<SyncCandlesCommand>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICexService _cexService = cexService;
        private readonly IMapper _mapper = mapper;

        public async Task Handle(SyncCandlesCommand command, CancellationToken cancellationToken)
        {
            var cexPrices = await _cexService.GetPrices(command.AccessToken);
            var minSession = cexPrices.Min(x => x.Session);
            var rmCandles = await _cexDbContext.Candles.Where(x => x.Session >= minSession).ToListAsync(cancellationToken);
            _cexDbContext.Candles.RemoveRange(rmCandles);

            var candles = _mapper.Map<List<Domain.Candle>>(cexPrices);
            _cexDbContext.Candles.AddRange(candles);

            await _cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
