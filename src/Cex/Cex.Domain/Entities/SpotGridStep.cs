using System.ComponentModel;

namespace Cex.Domain.Entities
{
    public class SpotGridStep
    {
        public long Id { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal Qty { get; set; }
        public string? OrderId { get; set; }
        public SpotGridStepStatus Status { get; set; }
        public DateTime? DeletedAt { get; set; }
        public ICollection<SpotOrder> Orders { get; private set; } = new List<SpotOrder>();
    }

    public enum SpotGridStepStatus
    {
        [Description("Awaiting Buy")] AwaitingBuy, // Bot is waiting for market price to approach entry price

        [Description("Buy Order Placed")] BuyOrderPlaced, // Buy order has been placed successfully

        [Description("Awaiting Sell")] AwaitingSell, // Bot is waiting for market price to approach take-profit price

        [Description("Sell Order Placed")] SellOrderPlaced // Sell order has been placed successfully
    }
}