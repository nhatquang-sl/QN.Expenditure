using Refit;

namespace Application.Common.ExServices.Bnb.Models
{
    public class AllOrdersRequest : SignatureRequest
    {
        [AliasAs("symbol")]
        public string Symbol { get; set; }

        [AliasAs("timestamp")]
        public long Timestamp { get; set; }

        [AliasAs("recvWindow")]
        public long RecvWindow { get; private set; }

        [AliasAs("startTime")]
        public long StartTime { get; set; }

        public AllOrdersRequest(string symbol, long timestamp, long startTime, string secretKey)
        {
            Symbol = symbol;
            Timestamp = timestamp;
            RecvWindow = 60000;
            StartTime = startTime;
            Signature = Sign($"symbol={Symbol}&timestamp={Timestamp}&recvWindow={RecvWindow}&startTime={startTime}", secretKey);
        }
    }
}
