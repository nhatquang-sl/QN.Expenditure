using Application.Common.Abstractions;
using AutoMapper;
using Lib.ExternalServices.Bnb.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Queries.GetSpotOrdersBySymbol
{
    public record GetSpotOrdersBySymbolQuery(string Symbol) : IRequest<List<SpotOrderRaw>>;

    public class GetSpotOrdersBySymbolQueryHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetSpotOrdersBySymbolQuery, List<SpotOrderRaw>>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<List<SpotOrderRaw>> Handle(GetSpotOrdersBySymbolQuery request, CancellationToken cancellationToken)
        {
            var settings = await _applicationDbContext.SpotOrders
                .Where(x => x.UserId == _currentUser.Id && x.Symbol == request.Symbol)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderRaw>>(settings) ?? [];
        }
    }
}
