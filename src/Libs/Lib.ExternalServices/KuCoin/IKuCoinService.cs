using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Refit;

namespace Lib.ExternalServices.KuCoin
{
    public interface IKuCoinService
    {
        public async Task<string> PlaceOrder(
            OrderRequest order, KuCoinConfig credentials
        )
        {
            var body = new Dictionary<string, string>
            {
                { "clientOid", Guid.NewGuid().ToString() },
                { "symbol", order.Symbol.ToKcSymbol() },
                { "side", order.Side },
                { "type", order.Type },
                { "price", order.Price },
                { "size", order.Size }
            };

            var bodyStr = JsonSerializer.Serialize(body);
            var (signature, timestamp) = GenerateSignature(credentials.ApiSecret, "POST",
                "/api/v1/orders", bodyStr);

            var res = await PlaceOrder(body, credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);
            if (res.Data == null || string.IsNullOrWhiteSpace(res.Data.OrderId))
            {
                throw new Exception(res.Msg);
            }

            return res.Data.OrderId;
        }

        public async Task<OrderDetails> GetOrderDetails(
            string orderId,
            KuCoinConfig credentials
        )
        {
            var (signature, timestamp) = GenerateSignature(credentials.ApiSecret, "GET",
                $"/api/v1/orders/{orderId}");

            var res = await GetOrderDetails(orderId, credentials.ApiKey, signature,
                timestamp, credentials.ApiPassphrase);
            if (!string.IsNullOrWhiteSpace(res.Data.Symbol))
            {
                res.Data.Symbol = res.Data.Symbol.ToNormalSymbol();
            }

            return res.Data;
        }

        public Task<string> CancelOrder(
            string orderId,
            KuCoinConfig credentials
        )
        {
            var (signature, timestamp) = GenerateSignature(credentials.ApiSecret, "DELETE",
                $"/api/v1/orders/{orderId}");

            return CancelOrder(orderId, credentials.ApiKey, signature,
                timestamp, credentials.ApiPassphrase);
        }

        public async Task<string> GetOrders(
            string status,
            KuCoinConfig credentials
        )
        {
            var (signature, timestamp) =
                GenerateSignature(credentials.ApiSecret, "GET", $"/api/v1/orders?status={status}");

            var res = await GetOrders(status ?? "done", credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);
            return res;
        }

        public async Task<List<Kline>> GetKlines(string symbol, string type, DateTime startAt, DateTime endAt,
            KuCoinConfig credentials)
        {
            var startAtUnix = new DateTimeOffset(startAt).ToUnixTimeSeconds();
            var endAtUnix = new DateTimeOffset(endAt).ToUnixTimeSeconds();

            var (signature, timestamp) = GenerateSignature(credentials.ApiSecret, "GET",
                $"/api/v1/market/candles?symbol={symbol.ToKcSymbol()}&type={type}&startAt={startAt}&endAt={endAt}");
            var res = await GetKlines(symbol.ToKcSymbol(), type, startAtUnix, endAtUnix, credentials.ApiKey,
                signature, timestamp, credentials.ApiPassphrase);
            return res.Data.Select(x => new Kline
            {
                OpenTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(x[0])).UtcDateTime,
                OpenPrice = decimal.Parse(x[1]),
                ClosePrice = decimal.Parse(x[2]),
                HighestPrice = decimal.Parse(x[3]),
                LowestPrice = decimal.Parse(x[4]),
                Volume = decimal.Parse(x[5]),
                Amount = decimal.Parse(x[6])
            }).ToList();
        }

        [Get("/api/v1/market/candles?symbol={symbol}&type={type}&startAt={startAt}&endAt={endAt}")]
        Task<KuCoinResponse<string[][]>> GetKlines(
            string symbol, string type, long startAt, long endAt,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );

        [Get("/api/v1/orders/{orderId}")]
        Task<KuCoinResponse<OrderDetails>> GetOrderDetails(
            string orderId,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );

        [Delete("/api/v1/orders/{orderId}")]
        Task<string> CancelOrder(
            string orderId,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );

        // https://www.kucoin.com/docs/rest/spot-trading/orders/get-order-list
        [Get("/api/v1/orders")]
        Task<string> GetOrders(
            [Query("status")] string status,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );

        // https://www.kucoin.com/docs/rest/spot-trading/orders/place-order
        [Post("/api/v1/orders")]
        Task<KuCoinResponse<OrderResponse>> PlaceOrder([Body] Dictionary<string, string> order,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string signature,
            [Header("KC-API-TIMESTAMP")] string timestamp,
            [Header("KC-API-PASSPHRASE")] string passphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2");

        (string, string) GenerateSignature(string apiSecret, string method, string endpoint,
            string body = "")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var preHash = $"{timestamp}{method.ToUpper()}{endpoint}{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            return (Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash))), timestamp);
        }
    }

    public class Kline
    {
        public DateTime OpenTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
    }

    public class OrderRequest
    {
        public string Side { get; init; } // buy or sell

        public string Symbol { get; init; } // e.g., BTC-USDT

        public string Type { get; init; } // limit or market

        public string Price { get; init; } // required for limit orders

        public string Size { get; init; } // required for both limit and market orders
    }

    public class OrderResponse
    {
        public string OrderId { get; set; }
    }

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

    public class KuCoinResponse<T>
    {
        public string Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }
    }
}