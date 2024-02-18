using Application.Common.ExServices.Bnb.Models;

using Refit;

namespace Application.Common.ExServices.Bnb
{
    public interface IBndService
    {
        [Get("/api/v3/time")]
        Task<ServerTimeResponse> GetServerTime();

        //https://binance-docs.github.io/apidocs/spot/en/#all-orders-user_data
        [Get("/api/v3/allOrders")]
        Task<List<SpotOrderRaw>> AllOrders([Header("X-MBX-APIKEY")] string authorization, AllOrdersRequest request);
    }
}
