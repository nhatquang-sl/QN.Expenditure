using Application.BnbSpotGrid.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotGrid.Commands.DeleteSpotGrid
{
    public record DeleteSpotGridCommand(long Id) : IRequest<SpotGridDto>
    { }

    public class DeleteSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<DeleteSpotGridCommand, SpotGridDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotGridDto> Handle(DeleteSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.SpotGrids
                .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == _currentUser.Id, cancellationToken);

            if (entity != null)
            {
                entity.DeletedAt = DateTime.UtcNow;

                _applicationDbContext.SpotGrids.Remove(entity);
                await _applicationDbContext.SaveChangesAsync(cancellationToken);
            }

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}
