using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.ExternalServices.Bnb.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Queries.GetSpotOrders
{
    public record GetSpotOrdersQuery : IRequest<List<SpotOrderRaw>>;

    public class GetSpotOrdersQueryHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<GetSpotOrdersQuery, List<SpotOrderRaw>>
    {
        public async Task<List<SpotOrderRaw>> Handle(GetSpotOrdersQuery request, CancellationToken cancellationToken)
        {
            var orders = await cexDbContext.SpotOrders
                .Where(x => x.UserId == currentUser.Id)
                .OrderBy(x => x.Symbol)
                .ThenByDescending(x => x.WorkingTime)
                .ToListAsync(cancellationToken);

            return orders.Select(x => new SpotOrderRaw
            {
                Symbol = x.Symbol,
                OrderId = long.Parse(x.OrderId ?? "0"),
                ClientOrderId = x.ClientOrderId,
                Price = x.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
                OrigQty = x.OrigQty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TimeInForce = x.TimeInForce,
                Type = x.Type,
                Side = x.Side,
                IsWorking = x.IsWorking,
                Time = x.CreatedAt.ToUnixTimestampMilliseconds(),
                UpdateTime = x.UpdatedAt.ToUnixTimestampMilliseconds(),
                WorkingTime = x.WorkingTime.ToUnixTimestampMilliseconds()
            }).ToList();
        }
    }
}
