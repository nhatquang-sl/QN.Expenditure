using Application.Common.Abstractions;
using Domain.Entities;
using Lib.ExternalServices.Bnb;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotGrid.Commands.TradeSpotGrid
{
    public class TradeSpotGridCommand : IRequest
    {
    }

    public class TradeSpotGridCommandHandler(IBnbService bnbService, IApplicationDbContext applicationDbContext)
    : IRequestHandler<TradeSpotGridCommand>
    {
        private readonly IBnbService _bnbService = bnbService;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task Handle(TradeSpotGridCommand command, CancellationToken cancellationToken)
        {
            var spotGrids = await _applicationDbContext.SpotGrids.ToListAsync(cancellationToken);

            var symbols = spotGrids.Select(x => x.Symbol).Distinct().ToArray();
            if (!symbols.Any()) return;
            var prices = await _bnbService.GetTickerPrice(symbols);
            foreach (var spotGrid in spotGrids)
            {
                var price = prices.First(x => x.Symbol == spotGrid.Symbol);
                if (price.Price == 0) continue;

                switch (spotGrid.Status)
                {
                    case SpotGridStatus.NEW:
                        if (price.Price < spotGrid.TriggerPrice)
                        {
                            spotGrid.Status = SpotGridStatus.RUNNING;
                            _applicationDbContext.SpotGrids.Update(spotGrid);
                        }
                        break;

                    case SpotGridStatus.RUNNING:
                        if (price.Price >= spotGrid.TakeProfit)
                        {
                            spotGrid.Status = SpotGridStatus.TAKE_PROFIT;
                            _applicationDbContext.SpotGrids.Update(spotGrid);
                        }
                        else if (price.Price <= spotGrid.StopLoss)
                        {
                            spotGrid.Status = SpotGridStatus.STOP_LOSS;
                            _applicationDbContext.SpotGrids.Update(spotGrid);
                        }
                        break;
                }
            }


            await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}