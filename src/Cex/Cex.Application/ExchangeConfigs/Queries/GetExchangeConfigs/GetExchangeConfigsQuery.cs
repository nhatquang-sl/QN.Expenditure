using Cex.Application.Common.Abstractions;
using Cex.Application.ExchangeConfigs.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.ExchangeConfigs.Queries.GetExchangeConfigs
{
    public record GetExchangeConfigsQuery : IRequest<List<ExchangeConfigDto>>;

    public class GetExchangeConfigsQueryHandler(
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<GetExchangeConfigsQuery, List<ExchangeConfigDto>>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;

        public async Task<List<ExchangeConfigDto>> Handle(GetExchangeConfigsQuery request, CancellationToken cancellationToken)
        {
            var entities = await _cexDbContext.ExchangeConfigs
                .Where(x => x.UserId == _currentUser.Id)
                .OrderBy(x => x.ExchangeName)
                .ToListAsync(cancellationToken);

            return entities.Select(e => new ExchangeConfigDto(e)).ToList();
        }
    }
}
