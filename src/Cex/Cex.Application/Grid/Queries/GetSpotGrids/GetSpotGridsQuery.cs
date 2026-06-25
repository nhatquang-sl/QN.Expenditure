using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Queries.GetSpotGrids
{
    public record GetSpotGridsQuery : IRequest<List<SpotGridDto>>
    {
    }

    public class GetSpotGridsQueryHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotGridsQuery, List<SpotGridDto>>
    {
        public async Task<List<SpotGridDto>> Handle(GetSpotGridsQuery command, CancellationToken cancellationToken)
        {
            return await cexDbContext.SpotGrids
                .Where(x => x.UserId == currentUser.Id)
                .Select(x => new SpotGridDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Symbol = x.Symbol,
                    LowerPrice = x.LowerPrice,
                    UpperPrice = x.UpperPrice,
                    TriggerPrice = x.TriggerPrice,
                    NumberOfGrids = x.NumberOfGrids,
                    GridMode = x.GridMode,
                    Investment = x.Investment,
                    BaseBalance = x.BaseBalance,
                    QuoteBalance = x.QuoteBalance,
                    Profit = x.Profit,
                    TakeProfit = x.TakeProfit,
                    StopLoss = x.StopLoss,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);
        }
    }
}
