using System.Text.Json;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin.Models;
using Refit;

namespace Lib.ExternalServices.KuCoin
{
    public interface IFtKuCoinService
    {
        public async Task<string> PlaceOrder(
            FPlaceOrderRequest placeOrder, KuCoinConfig credentials
        )
        {
            var body = new Dictionary<string, string>
            {
                { "clientOid", placeOrder.Symbol.ClientOid() },
                { "symbol", placeOrder.Symbol.ToKcSymbol() },
                { "side", placeOrder.Side.GetDescription() },
                { "type", placeOrder.Type.GetDescription() },
                { "price", placeOrder.Price },
                { "size", placeOrder.Size.ToString() }
            };

            var bodyStr = JsonSerializer.Serialize(body);
            var (signature, timestamp) = credentials.ApiSecret.GenerateSignature("POST",
                "/api/v1/orders", bodyStr);

            var res = await PlaceOrder(body, credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);
            if (res.Data == null || string.IsNullOrWhiteSpace(res.Data.OrderId))
            {
                throw new Exception(res.Msg);
            }

            return res.Data.OrderId;
        }

        // https://www.kucoin.com/docs/rest/futures-trading/orders/place-take-profit-and-stop-loss-order
        /// <summary>
        ///     Gets orders with the specified status
        /// </summary>
        /// <param name="status">The status of orders to retrieve</param>
        /// <param name="credentials">The KuCoin API credentials</param>
        /// <returns>A string containing the order information</returns>
        public async Task<string> GetOrders(
            OrderStatus status,
            KuCoinConfig credentials
        )
        {
            var statusStr = status.GetDescription();
            var (signature, timestamp) =
                credentials.ApiSecret.GenerateSignature("GET", $"/api/v1/orders?status={statusStr}");

            var res = await GetOrders(statusStr, credentials.ApiKey, signature, timestamp,
                credentials.ApiPassphrase);
            return res;
        }

        // https://www.kucoin.com/docs/rest/futures-trading/orders/place-order
        [Post("/api/v1/orders")]
        Task<KuCoinResponse<PlaceOrderResponse>> PlaceOrder([Body] Dictionary<string, string> order,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string signature,
            [Header("KC-API-TIMESTAMP")] string timestamp,
            [Header("KC-API-PASSPHRASE")] string passphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2");

        // https://www.kucoin.com/docs/rest/futures-trading/orders/get-order-list
        /// <summary>
        /// </summary>
        /// <param name="status">active or done, done as default.</param>
        /// <param name="apiKey"></param>
        /// <param name="apiSign"></param>
        /// <param name="apiTimestamp"></param>
        /// <param name="apiPassphrase"></param>
        /// <param name="apiVersion"></param>
        /// <returns></returns>
        [Get("/api/v1/orders")]
        Task<string> GetOrders(
            [Query("status")] string status,
            [Header("KC-API-KEY")] string apiKey,
            [Header("KC-API-SIGN")] string apiSign,
            [Header("KC-API-TIMESTAMP")] string apiTimestamp,
            [Header("KC-API-PASSPHRASE")] string apiPassphrase,
            [Header("KC-API-VERSION")] string apiVersion = "2"
        );
    }
}