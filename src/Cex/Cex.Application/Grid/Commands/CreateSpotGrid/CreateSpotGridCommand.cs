using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
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

    public class CreateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<CreateSpotGridCommand, SpotGridDto>
    {
        public async Task<SpotGridDto> Handle(CreateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = mapper.Map<SpotGrid>(command);

            entity.UserId = currentUser.Id;
            entity.Status = SpotGridStatus.NEW;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            entity.TriggerPrice = command.TriggerPrice;
            entity.GridMode = command.GridMode;
            var range = command.UpperPrice - command.LowerPrice;
            var amount = range / command.NumberOfGrids;
            for (var i = 0; i < command.NumberOfGrids; i++)
            {
                entity.GridSteps.Add(new SpotGridStep
                {
                    BuyPrice = (command.LowerPrice + amount * i).RoundDownStd(),
                    SellPrice = (command.LowerPrice + amount * (i + 1)).RoundDownStd(),
                    Qty = (command.Investment / (command.LowerPrice + amount * i)).RoundDownStd(),
                    Status = SpotGridStepStatus.AwaitingBuy
                });
            }

            cexDbContext.SpotGrids.Add(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}