using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Commands.UpdateSpotGrid
{
    public record UpdateSpotGridCommand(
        long Id,
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

    public class UpdateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<UpdateSpotGridCommand, SpotGridDto>
    {
        public async Task<SpotGridDto> Handle(UpdateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await cexDbContext.SpotGrids
                             .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == currentUser.Id,
                                 cancellationToken)
                         ?? throw new NotFoundException("Grid is not found.");

            entity.LowerPrice = command.LowerPrice;
            entity.UpperPrice = command.UpperPrice;
            entity.TriggerPrice = command.TriggerPrice;
            entity.NumberOfGrids = command.NumberOfGrids;
            entity.GridMode = command.GridMode;
            entity.Investment = command.Investment;
            entity.TakeProfit = command.TakeProfit;
            entity.StopLoss = command.StopLoss;
            entity.UpdatedAt = DateTime.UtcNow;

            cexDbContext.SpotGrids.Update(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}