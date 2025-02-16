using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
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
        public async Task<SpotGridDto> Handle(UpdateSpotGridCommand command, CancellationToken cancellationToken)
        {
            logTrace.AddProperty("gridId", command.Id);
            var entity = await cexDbContext.SpotGrids
                             .Include(x => x.GridSteps)
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

            foreach (var entityGridStep in entity.GridSteps)
            {
                entityGridStep.DeletedAt = curDateTime;

                if (string.IsNullOrWhiteSpace(entityGridStep.OrderId))
                {
                    continue;
                }

                var res = await kuCoinService.CancelOrder(entityGridStep.OrderId, kuCoinConfig.Value);
                logTrace.LogInformation($"Cancel order {entityGridStep.OrderId} of step {entityGridStep.Id}", res);
            }

            var stepSize = (command.UpperPrice - command.LowerPrice) / command.NumberOfGrids;
            var investmentPerStep = command.Investment / command.NumberOfGrids;
            for (var i = 0; i < command.NumberOfGrids; i++)
            {
                entity.GridSteps.Add(new SpotGridStep
                {
                    BuyPrice = (command.LowerPrice + stepSize * i).RoundDownStd(),
                    SellPrice = (command.LowerPrice + stepSize * (i + 1)).RoundDownStd(),
                    Qty = investmentPerStep / (command.LowerPrice + stepSize * i),
                    Status = SpotGridStepStatus.AwaitingBuy
                });
            }

            cexDbContext.SpotGrids.Update(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}