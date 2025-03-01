using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Application.Grid.Shared.Extensions;
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
        private const decimal InitialPercent = 0.25m;

        public async Task<SpotGridDto> Handle(CreateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = mapper.Map<SpotGrid>(command);
            entity.UserId = currentUser.Id;
            entity.Status = SpotGridStatus.NEW;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            entity.TriggerPrice = command.TriggerPrice;
            entity.GridMode = command.GridMode;
            entity.Investment = command.Investment;
            entity.BaseBalance = 0;
            entity.QuoteBalance = command.Investment;
            entity.Profit = 0;
            var stepSize = (command.UpperPrice - command.LowerPrice) / command.NumberOfGrids;
            var investmentPerStep = ((1 - InitialPercent) * command.Investment / command.NumberOfGrids).FixedNumber();

            entity.AddNormalSteps();
            entity.AddOrUpdateInitialStep();
            entity.AddTakeProfitStep();
            entity.AddStopLossStep();

            cexDbContext.SpotGrids.Add(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}