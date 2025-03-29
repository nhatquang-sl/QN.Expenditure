namespace Lib.ExternalServices.KuCoin.Models
{
    public class OrderDetails
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public string OpType { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public string Price { get; set; }
        public string Size { get; set; }
        public string Funds { get; set; }
        public string DealFunds { get; set; }
        public string DealSize { get; set; }
        public string Fee { get; set; }
        public string FeeCurrency { get; set; }
        public string Stp { get; set; }
        public string Stop { get; set; }
        public bool StopTriggered { get; set; }
        public string StopPrice { get; set; }
        public string TimeInForce { get; set; }
        public bool PostOnly { get; set; }
        public bool Hidden { get; set; }
        public bool Iceberg { get; set; }
        public string VisibleSize { get; set; }
        public int CancelAfter { get; set; }
        public string Channel { get; set; }
        public string ClientOid { get; set; }
        public string Remark { get; set; }
        public string Tags { get; set; }
        public bool IsActive { get; set; }
        public bool CancelExist { get; set; }
        public long CreatedAt { get; set; }
        public string TradeType { get; set; }
    }
}