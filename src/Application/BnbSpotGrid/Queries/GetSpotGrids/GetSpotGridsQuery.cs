using Application.BnbSpotGrid.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotGrid.Queries.GetSpotGrids
{
    public record GetSpotGridsQuery : IRequest<List<SpotGridDto>> { }

    public class GetSpotGridsQueryHandler(IMapper mapper, ICurrentUser currentUser
        , IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetSpotGridsQuery, List<SpotGridDto>>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<List<SpotGridDto>> Handle(GetSpotGridsQuery command, CancellationToken cancellationToken)
        {
            var entities = await _applicationDbContext.SpotGrids
                .Where(x => x.UserId == _currentUser.Id && x.DeletedAt == null)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotGridDto>>(entities) ?? [];
        }
    }
}
