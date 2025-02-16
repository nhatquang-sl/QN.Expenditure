using AutoMapper;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Queries.GetSpotGrids
{
    public record GetSpotGridsQuery : IRequest<List<SpotGridDto>>
    {
    }

    public class GetSpotGridsQueryHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotGridsQuery, List<SpotGridDto>>
    {
        public async Task<List<SpotGridDto>> Handle(GetSpotGridsQuery command, CancellationToken cancellationToken)
        {
            var entities = await cexDbContext.SpotGrids
                .Where(x => x.UserId == currentUser.Id)
                .ToListAsync(cancellationToken);

            return mapper.Map<List<SpotGridDto>>(entities) ?? [];
        }
    }
}