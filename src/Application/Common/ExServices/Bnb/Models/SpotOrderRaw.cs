namespace Application.Common.ExServices.Bnb.Models
{
    public class SpotOrderRaw
    {
        public string Symbol { get; set; }
        public long OrderId { get; set; }
        public int OrderListId { get; set; } //Unless OCO, the value will always be -1
        public string ClientOrderId { get; set; }
        public string Price { get; set; }
        public string OrigQty { get; set; }
        public string ExecutedQty { get; set; }
        public string CummulativeQuoteQty { get; set; }
        public string Status { get; set; }
        public string TimeInForce { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public string StopPrice { get; set; }
        public string IcebergQty { get; set; }
        public long Time { get; set; }
        public long UpdateTime { get; set; }
        public bool IsWorking { get; set; }
        public long WorkingTime { get; set; }
        public string OrigQuoteOrderQty { get; set; }
        public string SelfTradePreventionMode { get; set; }
    }
}
