namespace Cex.Domain.Entities
{
    public class SpotOrder
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public string OrderId { get; set; }

        public string? ClientOrderId { get; set; }
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public string? TimeInForce { get; set; }
        public string? Type { get; set; }
        public string? Side { get; set; }
        public decimal Fee { get; set; }
        public string? FeeCurrency { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsWorking { get; set; }
        public DateTime WorkingTime { get; set; }
        public long SpotGridOrderId { get; set; }
    }
}