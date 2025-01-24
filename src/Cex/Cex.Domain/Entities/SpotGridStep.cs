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

        public ICollection<SpotOrder> Orders { get; private set; } = new List<SpotOrder>();
    }

    public enum SpotGridStepStatus
    {
        AwaitingBuy, // Bot is waiting for market price to approach entry price
        BuyOrderPlaced, // Buy order has been placed successfully
        AwaitingSell, // Bot is waiting for market price to approach take-profit price
        SellOrderPlaced // Sell order has been placed successfully
    }
}