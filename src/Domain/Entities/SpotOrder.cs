namespace Domain.Entities
{
    public class SpotOrder
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public long OrderId { get; set; }
        public int OrderListId { get; set; } //Unless OCO, the value will always be -1
        public string ClientOrderId { get; set; }
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public decimal ExecutedQty { get; set; }
        public decimal CummulativeQuoteQty { get; set; }
        public string Status { get; set; }
        public string TimeInForce { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public decimal StopPrice { get; set; }
        public decimal IcebergQty { get; set; }
        public DateTime Time { get; set; }
        public DateTime UpdateTime { get; set; }
        public bool IsWorking { get; set; }
        public DateTime WorkingTime { get; set; }
        public decimal OrigQuoteOrderQty { get; set; }
        public string SelfTradePreventionMode { get; set; }
    }
}
