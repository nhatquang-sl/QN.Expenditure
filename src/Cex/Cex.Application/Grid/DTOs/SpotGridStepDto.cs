using Cex.Domain.Entities;
using Lib.Application.Extensions;

namespace Cex.Application.Grid.DTOs
{
    public class SpotGridStepDto
    {
        public long Id { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal Qty { get; set; }
        public string? OrderId { get; set; }
        public string Status { get; set; }

        public static SpotGridStepDto From(SpotGridStep entity) => new()
        {
            Id = entity.Id,
            BuyPrice = entity.BuyPrice,
            SellPrice = entity.SellPrice,
            Qty = entity.Qty,
            OrderId = entity.OrderId,
            Status = entity.Status.GetDescription()
        };
    }
}
