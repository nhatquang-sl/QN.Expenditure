using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Application.Grid.Shared.Extensions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.UpdateSpotGrid
{
    public class UpdateSpotGridCommand : IRequest<SpotGridDto>
    {
        public long Id { get; set; }
        public decimal LowerPrice { get; set; }
        public decimal UpperPrice { get; set; }
        public decimal TriggerPrice { get; set; }
        public int NumberOfGrids { get; set; }
        public SpotGridMode GridMode { get; set; }
        public decimal Investment { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? StopLoss { get; set; }
    }

    public class UpdateSpotGridCommandHandler(
        IMapper mapper,
        ILogTrace logTrace,
        ICurrentUser currentUser,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig
    )
        : IRequestHandler<UpdateSpotGridCommand, SpotGridDto>
    {
        private readonly DateTime _currentDateTime = DateTime.UtcNow;

        public async Task<SpotGridDto> Handle(UpdateSpotGridCommand command, CancellationToken cancellationToken)
        {
            logTrace.AddProperty("gridId", command.Id);
            var entity = await cexDbContext.SpotGrids
                             .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == currentUser.Id,
                                 cancellationToken)
                         ?? throw new NotFoundException("Grid is not found.");

            var curDateTime = DateTime.UtcNow;
            entity.LowerPrice = command.LowerPrice;
            entity.UpperPrice = command.UpperPrice;
            entity.TriggerPrice = command.TriggerPrice;
            entity.NumberOfGrids = command.NumberOfGrids;
            entity.GridMode = command.GridMode;
            entity.Investment = command.Investment;
            entity.TakeProfit = command.TakeProfit;
            entity.StopLoss = command.StopLoss;
            entity.UpdatedAt = curDateTime;
            var steps = cexDbContext.SpotGridSteps
                .Where(s => s.SpotGridId == entity.Id)
                .ToList();
            await UpdateInitialStep(entity, steps.First(x => x.Type == SpotGridStepType.Initial));
            await UpdateTakeProfitStep(entity, steps.FirstOrDefault(x => x.Type == SpotGridStepType.TakeProfit));
            await UpdateStopLossStep(entity, steps.FirstOrDefault(x => x.Type == SpotGridStepType.StopLoss));
            await CancelAllOrders(entity);

            entity.AddNormalSteps();

            cexDbContext.SpotGrids.Update(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }

        private async Task CancelAllOrders(SpotGrid grid)
        {
            var steps = cexDbContext.SpotGridSteps
                .Where(s => s.SpotGridId == grid.Id).ToList()
                .Select(async step =>
                {
                    step.DeletedAt = _currentDateTime;
                    if (string.IsNullOrWhiteSpace(step.OrderId))
                    {
                        return;
                    }

                    var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
                    logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
                });
            await Task.WhenAll(steps);
        }

        private async Task UpdateInitialStep(SpotGrid entity, SpotGridStep initStep)
        {
            switch (initStep.Status)
            {
                case SpotGridStepStatus.AwaitingBuy:
                case SpotGridStepStatus.BuyOrderPlaced:
                    await CancelKuCoinOrder(initStep);
                    entity.AddOrUpdateInitialStep(initStep);
                    break;
                case SpotGridStepStatus.AwaitingSell:
                case SpotGridStepStatus.SellOrderPlaced:
                default:
                    return;
            }
        }

        private async Task UpdateTakeProfitStep(SpotGrid entity, SpotGridStep? takeProfitStep)
        {
            if (takeProfitStep == null)
            {
                entity.AddTakeProfitStep();
                return;
            }

            // Take Profit Price unchanged
            if (entity.TakeProfit.HasValue &&
                takeProfitStep.BuyPrice == entity.TakeProfit.Value.FixedNumber())
            {
                return;
            }

            switch (takeProfitStep.Status)
            {
                case SpotGridStepStatus.AwaitingBuy:
                case SpotGridStepStatus.BuyOrderPlaced:
                    await CancelKuCoinOrder(takeProfitStep);
                    entity.UpdateOrDeleteTakeProfitStep(takeProfitStep, _currentDateTime);
                    break;
                case SpotGridStepStatus.AwaitingSell:
                case SpotGridStepStatus.SellOrderPlaced:
                default:
                    return;
            }
        }

        private async Task UpdateStopLossStep(SpotGrid entity, SpotGridStep? stopLossStep)
        {
            if (stopLossStep == null)
            {
                entity.AddStopLossStep();
                return;
            }

            // Stop Loss Price unchanged
            if (entity.StopLoss.HasValue &&
                stopLossStep.BuyPrice == entity.StopLoss.Value.FixedNumber())
            {
                return;
            }

            switch (stopLossStep.Status)
            {
                case SpotGridStepStatus.AwaitingBuy:
                case SpotGridStepStatus.BuyOrderPlaced:
                    await CancelKuCoinOrder(stopLossStep);
                    entity.UpdateOrDeleteStopLossStep(stopLossStep, _currentDateTime);
                    return;
                case SpotGridStepStatus.AwaitingSell:
                case SpotGridStepStatus.SellOrderPlaced:
                default:
                    return;
            }
        }

        private async Task CancelKuCoinOrder(SpotGridStep step)
        {
            if (string.IsNullOrWhiteSpace(step.OrderId))
            {
                return;
            }

            var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
            logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
        }
    }
}