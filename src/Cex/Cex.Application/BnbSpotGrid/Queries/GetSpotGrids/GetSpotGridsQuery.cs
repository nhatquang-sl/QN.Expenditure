using AutoMapper;
using Cex.Application.BnbSpotGrid.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotGrid.Queries.GetSpotGrids
{
    public record GetSpotGridsQuery : IRequest<List<SpotGridDto>>
    {
    }

    public class GetSpotGridsQueryHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotGridsQuery, List<SpotGridDto>>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<List<SpotGridDto>> Handle(GetSpotGridsQuery command, CancellationToken cancellationToken)
        {
            var entities = await _cexDbContext.SpotGrids
                .Where(x => x.UserId == _currentUser.Id && x.DeletedAt == null)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotGridDto>>(entities) ?? [];
        }
    }
}