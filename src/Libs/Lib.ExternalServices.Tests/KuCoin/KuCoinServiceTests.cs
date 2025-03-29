using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using Microsoft.Extensions.Options;

namespace Lib.ExternalServices.Tests.KuCoin
{
    public class KuCoinServiceTests : DependencyInjectionFixture
    {
        [Fact]
        public async Task PlaceOrder_GetOrders_CancelOrder()
        {
            var config = GetService<IOptions<KuCoinConfig>>();
            var kuCoinConfig = config.Value;
            var kuCoinService = GetService<IKuCoinService>();

            var orderId = await kuCoinService.PlaceOrder(new PlaceOrderRequest
            {
                Symbol = "BTC-USDT",
                Side = "buy",
                Type = "limit",
                Price = "100",
                Size = "0.01"
            }, kuCoinConfig);
            Assert.NotNull(orderId);
            Assert.NotEmpty(orderId);

            var order = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig);
            Assert.NotNull(order);
            Assert.Equal(orderId, order.Id);

            var res = await kuCoinService.GetOrders("done", kuCoinConfig);
            Assert.NotNull(res);
            Assert.True(res.Length > 0);

            var cancelRes = await kuCoinService.CancelOrder(orderId, kuCoinConfig);
            Assert.Contains(orderId, cancelRes);
        }

        [Fact]
        public async Task GetAccounts()
        {
            var config = GetService<IOptions<KuCoinConfig>>();
            var kuCoinConfig = config.Value;
            var kuCoinService = GetService<IKuCoinService>();
            var accounts = await kuCoinService.GetAccounts("trade", "USDT", kuCoinConfig);

            Assert.NotNull(accounts);
            var account = Assert.Single(accounts);
            Assert.NotNull(account.Id);
            Assert.Equal("trade", account.Type);
            Assert.Equal("USDT", account.Currency);
        }
    }
}