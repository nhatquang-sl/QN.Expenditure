using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.ExternalServices.Bnb;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotGrid.Commands.TradeSpotGrid
{
    public class TradeSpotGridCommand : IRequest
    {
    }

    public class TradeSpotGridCommandHandler(IBnbService bnbService, ICexDbContext cexDbContext)
        : IRequestHandler<TradeSpotGridCommand>
    {
        private readonly IBnbService _bnbService = bnbService;
        private readonly ICexDbContext _cexDbContext = cexDbContext;

        public async Task Handle(TradeSpotGridCommand command, CancellationToken cancellationToken)
        {
            var spotGrids = await _cexDbContext.SpotGrids.ToListAsync(cancellationToken);

            var symbols = spotGrids.Select(x => x.Symbol).Distinct().ToArray();
            if (symbols.Length == 0)
            {
                return;
            }

            var prices = await _bnbService.GetTickerPrice(symbols);
            foreach (var spotGrid in spotGrids)
            {
                var price = prices.First(x => x.Symbol == spotGrid.Symbol);
                if (price.Price == 0)
                {
                    continue;
                }

                switch (spotGrid.Status)
                {
                    case SpotGridStatus.NEW:
                        if (price.Price < spotGrid.TriggerPrice)
                        {
                            spotGrid.Status = SpotGridStatus.RUNNING;
                            _cexDbContext.SpotGrids.Update(spotGrid);
                        }

                        break;

                    case SpotGridStatus.RUNNING:
                        if (price.Price >= spotGrid.TakeProfit)
                        {
                            spotGrid.Status = SpotGridStatus.TAKE_PROFIT;
                            _cexDbContext.SpotGrids.Update(spotGrid);
                        }
                        else if (price.Price <= spotGrid.StopLoss)
                        {
                            spotGrid.Status = SpotGridStatus.STOP_LOSS;
                            _cexDbContext.SpotGrids.Update(spotGrid);
                        }

                        break;
                }
            }


            await _cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}