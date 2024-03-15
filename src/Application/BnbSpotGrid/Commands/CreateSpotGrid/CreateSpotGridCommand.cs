using Application.BnbSpotGrid.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.BnbSpotGrid.Commands.CreateSpotGrid
{
    public record CreateSpotGridCommand(string Symbol
        , decimal LowerPrice, decimal UpperPrice, decimal TriggerPrice
        , int NumberOfGrids, SpotGridMode GridMode, decimal Investment
        , decimal TakeProfit, decimal StopLoss) : IRequest<SpotGridDto>
    { }

    public class CreateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
    : IRequestHandler<CreateSpotGridCommand, SpotGridDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotGridDto> Handle(CreateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = _mapper.Map<SpotGrid>(command);

            entity.UserId = _currentUser.Id;
            entity.Status = SpotGridStatus.NEW;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            _applicationDbContext.SpotGrids.Add(entity);
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}
