using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Refit;

namespace Lib.ExternalServices.KuCoin
{
    public interface IKuCoinService
    {
        public Task<string> PlaceOrder(
            OrderRequest order,
            string apiKey,
            string apiSecret,
            string passphrase
        )
        {
            var body = new Dictionary<string, string>
            {
                { "clientOid", Guid.NewGuid().ToString() },
                { "symbol", order.Symbol },
                { "side", order.Side },
                { "type", order.Type },
                { "price", order.Price },
                { "size", order.Size }
            };

            var bodyStr = JsonSerializer.Serialize(body);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var signature = GenerateSignature(apiSecret, timestamp, "POST", "/api/v1/orders",
                bodyStr);

            return PlaceOrder(body, apiKey, signature, timestamp, passphrase);
        }

        [Post("/api/v1/orders")]
        internal Task<string> PlaceOrder([Body] Dictionary<string, string> order,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string signature,
            [Header("KC-API-TIMESTAMP")] string timestamp,
            [Header("KC-API-PASSPHRASE")] string passphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2");

        internal string GenerateSignature(string apiSecret, string timestamp, string method, string endpoint,
            string body)
        {
            var preHash = $"{timestamp}{method.ToUpper()}{endpoint}{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash)));
        }
    }

    public class OrderRequest
    {
        public string ClientOid { get; set; } // Unique order ID (UUID recommended)

        public string Side { get; init; } // buy or sell

        public string Symbol { get; init; } // e.g., BTC-USDT

        public string Type { get; init; } // limit or market

        public string Price { get; init; } // required for limit orders

        public string Size { get; init; } // required for both limit and market orders
    }
}