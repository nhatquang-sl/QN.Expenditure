using Application.Common.Abstractions;
using Application.Common.ExServices.Bnb.Models;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Queries.GetSpotOrders
{
    public record GetSpotOrdersQuery : IRequest<List<SpotOrderRaw>>;

    public class GetSpotOrdersQueryHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetSpotOrdersQuery, List<SpotOrderRaw>>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<List<SpotOrderRaw>> Handle(GetSpotOrdersQuery request, CancellationToken cancellationToken)
        {
            var settings = await _applicationDbContext.SpotOrders
                .Where(x => x.UserId == _currentUser.Id)
                .OrderBy(x => x.Symbol)
                .ThenByDescending(x => x.WorkingTime)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderRaw>>(settings) ?? [];
        }
    }
}
