using AutoMapper;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.ExternalServices.Bnb.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Queries.GetSpotOrders
{
    public record GetSpotOrdersQuery : IRequest<List<SpotOrderRaw>>;

    public class GetSpotOrdersQueryHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotOrdersQuery, List<SpotOrderRaw>>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<List<SpotOrderRaw>> Handle(GetSpotOrdersQuery request, CancellationToken cancellationToken)
        {
            var settings = await _cexDbContext.SpotOrders
                .Where(x => x.UserId == _currentUser.Id)
                .OrderBy(x => x.Symbol)
                .ThenByDescending(x => x.WorkingTime)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderRaw>>(settings) ?? [];
        }
    }
}