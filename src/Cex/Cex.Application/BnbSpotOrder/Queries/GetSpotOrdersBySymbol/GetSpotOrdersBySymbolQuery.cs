using AutoMapper;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.ExternalServices.Bnb.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Queries.GetSpotOrdersBySymbol
{
    public record GetSpotOrdersBySymbolQuery(string Symbol) : IRequest<List<SpotOrderRaw>>;

    public class GetSpotOrdersBySymbolQueryHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotOrdersBySymbolQuery, List<SpotOrderRaw>>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<List<SpotOrderRaw>> Handle(GetSpotOrdersBySymbolQuery request,
            CancellationToken cancellationToken)
        {
            var settings = await _cexDbContext.SpotOrders
                .Where(x => x.UserId == _currentUser.Id && x.Symbol == request.Symbol)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderRaw>>(settings) ?? [];
        }
    }
}