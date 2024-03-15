using Application.BnbSpotGrid.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotGrid.Commands.UpdateSpotGrid
{
    public record UpdateSpotGridCommand(long Id
        , decimal LowerPrice, decimal UpperPrice, decimal TriggerPrice
        , int NumberOfGrids, SpotGridMode GridMode, decimal Investment
        , decimal TakeProfit, decimal StopLoss) : IRequest<SpotGridDto>
    { }

    public class UpdateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<UpdateSpotGridCommand, SpotGridDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotGridDto> Handle(UpdateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.SpotGrids
                .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == _currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"Grid is not found.");

            entity.LowerPrice = command.LowerPrice;
            entity.UpperPrice = command.UpperPrice;
            entity.TriggerPrice = command.TriggerPrice;
            entity.NumberOfGrids = command.NumberOfGrids;
            entity.GridMode = command.GridMode;
            entity.Investment = command.Investment;
            entity.TakeProfit = command.TakeProfit;
            entity.StopLoss = command.StopLoss;
            entity.UpdatedAt = DateTime.UtcNow;

            _applicationDbContext.SpotGrids.Update(entity);
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}
