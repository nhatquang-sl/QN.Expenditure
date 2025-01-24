using Lib.ExternalServices.KuCoin;
using Microsoft.Extensions.Options;

namespace Lib.ExternalServices.Tests.KuCoin
{
    public class KuCoinServiceTests : DependencyInjectionFixture
    {
        [Fact]
        public async void PlaceOrder_GetOrders_CancelOrder()
        {
            var config = GetService<IOptions<KuCoinConfig>>();
            var kuCoinConfig = config.Value;
            var kuCoinService = GetService<IKuCoinService>();
            var orderId = await kuCoinService.PlaceOrder(new OrderRequest
            {
                Symbol = "BTC-USDT",
                Side = "buy",
                Type = "limit",
                Price = "100",
                Size = "0.01"
            }, kuCoinConfig);

            var order = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig);

            var res = await kuCoinService.GetOrders("done", kuCoinConfig);
        }
    }
}