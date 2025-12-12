using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin.Models;
using Refit;

namespace Lib.ExternalServices.KuCoin
{
    public interface IKuCoinService
    {
        public async Task<string> PlaceOrder(
            PlaceOrderRequest placeOrder, KuCoinConfig credentials
        )
        {
            var body = new Dictionary<string, string>
            {
                { "clientOid", Guid.NewGuid().ToString() },
                { "symbol", placeOrder.Symbol.ToKcSymbol() },
                { "side", placeOrder.Side },
                { "type", placeOrder.Type },
                { "price", placeOrder.Price },
                { "size", placeOrder.Size }
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

        public async Task<TradeHistoryResponse> GetTradeHistory(string symbol,
            DateTime fromDate,
            KuCoinConfig credentials)
        {
            // var startAt = (new DateTime(2025,9,20)).ToUnixTimestampMilliseconds();
            var startAt = fromDate.ToUnixTimestampMilliseconds();
            var (signature, timestamp) =
                GenerateSignature(credentials.ApiSecret, "GET",
                    $"/api/v1/fills?symbol={symbol}&startAt={startAt}");

            var res = await GetTradeHistory(symbol, startAt, credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);

            return res.Data;
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
            }).OrderBy(x => x.OpenTime).ToList();
        }

        public async Task<List<Account>> GetAccounts(string type, string currency, KuCoinConfig credentials)
        {
            var (signature, timestamp) = GenerateSignature(credentials.ApiSecret, "GET",
                $"/api/v1/accounts?type={type}&currency={currency}");
            var res = await GetAccounts(type, currency, credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);

            return res.Data.ToList();
        }

        // https://www.kucoin.com/docs/rest/spot-trading/market-data/get-klines
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

        // https://www.kucoin.com/docs-new/rest/spot-trading/orders/get-trade-history
        [Get("/api/v1/fills")]
        Task<KuCoinResponse<TradeHistoryResponse>> GetTradeHistory
        (
            [Query("symbol")] string symbol,
            [Query("startAt")] long startAt,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );

        // https://www.kucoin.com/docs/rest/spot-trading/orders/place-order
        [Post("/api/v1/orders")]
        Task<KuCoinResponse<PlaceOrderResponse>> PlaceOrder([Body] Dictionary<string, string> order,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string signature,
            [Header("KC-API-TIMESTAMP")] string timestamp,
            [Header("KC-API-PASSPHRASE")] string passphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2");

        // https://www.kucoin.com/docs/rest/account/basic-info/get-account-list-spot-margin-trade_hf
        [Get("/api/v1/accounts?type={type}&currency={currency}")]
        Task<KuCoinResponse<Account[]>> GetAccounts(
            string type, string currency,
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
}