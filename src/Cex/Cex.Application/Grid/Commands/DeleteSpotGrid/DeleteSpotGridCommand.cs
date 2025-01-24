using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Commands.DeleteSpotGrid
{
    public record DeleteSpotGridCommand(long Id) : IRequest<SpotGridDto>
    {
    }

    public class DeleteSpotGridCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<DeleteSpotGridCommand, SpotGridDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<SpotGridDto> Handle(DeleteSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await _cexDbContext.SpotGrids
                .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == _currentUser.Id, cancellationToken);

            if (entity == null)
            {
                return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
            }

            entity.DeletedAt = DateTime.UtcNow;

            _cexDbContext.SpotGrids.Update(entity);
            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotGridDto>(entity) ?? new SpotGridDto();
        }
    }
}