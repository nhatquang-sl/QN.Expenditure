namespace Lib.ExternalServices.KuCoin.Models
{
    public class Account
    {
        public string Id { get; set; }
        public string Currency { get; set; }
        public string Type { get; set; } // "main", "trade", "margin", "isolated", etc.
        public string Balance { get; set; }
        public string Available { get; set; }
        public string Holds { get; set; }
    }
}