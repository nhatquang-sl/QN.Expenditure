using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Queries.GetSpotGridById
{
    public record GetSpotGridByIdQuery(long Id) : IRequest<SpotGridDto>
    {
    }

    public class GetSpotGridByIdQueryHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotGridByIdQuery, SpotGridDto>
    {
        public async Task<SpotGridDto> Handle(GetSpotGridByIdQuery command, CancellationToken cancellationToken)
        {
            var entity = await cexDbContext.SpotGrids
                             .Include(x => x.GridSteps)
                             .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.Id == command.Id,
                                 cancellationToken)
                         ?? throw new NotFoundException("The Grid could not be found");

            return mapper.Map<SpotGridDto>(entity);
        }
    }
}