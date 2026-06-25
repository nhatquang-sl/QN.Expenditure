using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Application.Grid.Shared.Extensions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using MediatR;

namespace Cex.Application.Grid.Commands.CreateSpotGrid
{
    public record CreateSpotGridCommand(
        string Symbol,
        decimal LowerPrice,
        decimal UpperPrice,
        decimal TriggerPrice,
        int NumberOfGrids,
        SpotGridMode GridMode,
        decimal Investment,
        decimal? TakeProfit = null,
        decimal? StopLoss = null) : IRequest<SpotGridDto>
    {
    }

    public class CreateSpotGridCommandHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<CreateSpotGridCommand, SpotGridDto>
    {
        public async Task<SpotGridDto> Handle(CreateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = new SpotGrid
            {
                UserId = currentUser.Id,
                Symbol = command.Symbol,
                LowerPrice = command.LowerPrice,
                UpperPrice = command.UpperPrice,
                TriggerPrice = command.TriggerPrice,
                NumberOfGrids = command.NumberOfGrids,
                GridMode = command.GridMode,
                Investment = command.Investment,
                TakeProfit = command.TakeProfit,
                StopLoss = command.StopLoss,
                Status = SpotGridStatus.NEW,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                BaseBalance = 0,
                QuoteBalance = command.Investment,
                Profit = 0
            };

            entity.AddNormalSteps();
            entity.AddOrUpdateInitialStep();
            entity.AddTakeProfitStep();
            entity.AddStopLossStep();

            cexDbContext.SpotGrids.Add(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return SpotGridDto.From(entity);
        }
    }
}
