using AutoMapper;
using Cex.Application.BnbSpotGrid.DTOs;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using MediatR;

namespace Cex.Application.BnbSpotGrid.Commands.CreateSpotGrid
{
    public record CreateSpotGridCommand(
        string Symbol,
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

    public class CreateSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<CreateSpotGridCommand, SpotGridDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<SpotGridDto> Handle(CreateSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = _mapper.Map<SpotGrid>(command);

            entity.UserId = _currentUser.Id;
            entity.Status = SpotGridStatus.NEW;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            _cexDbContext.SpotGrids.Add(entity);
            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}