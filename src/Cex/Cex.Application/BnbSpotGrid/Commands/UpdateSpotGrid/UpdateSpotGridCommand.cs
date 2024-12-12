using AutoMapper;
using Cex.Application.BnbSpotGrid.DTOs;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotGrid.Commands.UpdateSpotGrid
{
    public record UpdateSpotGridCommand(
        long Id,
        decimal LowerPrice,
        decimal UpperPrice,
        decimal TriggerPrice,
        int NumberOfGrids,
        SpotGridMode GridMode,
        decimal Investment,
        decimal TakeProfit,
        decimal StopLoss) : IRequest<SpotGridDto>
    {
    }

    public class UpdateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<UpdateSpotGridCommand, SpotGridDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<SpotGridDto> Handle(UpdateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await _cexDbContext.SpotGrids
                             .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == _currentUser.Id,
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

            _cexDbContext.SpotGrids.Update(entity);
            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}