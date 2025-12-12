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

        [Fact]
        public async Task GetTradeHistory()
        {
            var config = GetService<IOptions<KuCoinConfig>>();
            var kuCoinConfig = config.Value;
            var kuCoinService = GetService<IKuCoinService>();
            var fromDate = new DateTime(2025, 9, 20);
            var tradeHis = await kuCoinService.GetTradeHistory("XAUT-USDT", fromDate, kuCoinConfig);

            // Assert response is not null and contains items
            Assert.NotNull(tradeHis);
            Assert.NotNull(tradeHis.Items);
            Assert.NotEmpty(tradeHis.Items);

            // Get the first trade item for detailed assertions
            var firstTrade = tradeHis.Items.First();

            // Assert basic trade properties
            Assert.Equal("XAUT-USDT", firstTrade.Symbol);
            Assert.Equal("16139648684271617", firstTrade.TradeId);
            Assert.Equal("68cfe73e17df15000889b7cf", firstTrade.OrderId);
            Assert.Equal("68cfd97217df1500085c637e", firstTrade.CounterOrderId);

            // Assert trade details
            Assert.Equal("buy", firstTrade.Side);
            Assert.Equal("taker", firstTrade.Liquidity);
            Assert.False(firstTrade.ForceTaker);
            Assert.Equal("limit", firstTrade.Type);
            Assert.Equal("TRADE", firstTrade.TradeType);

            // Assert price and size
            Assert.Equal("3679.48", firstTrade.Price);
            Assert.Equal("0.1", firstTrade.Size);
            Assert.Equal("367.948", firstTrade.Funds);

            // Assert fee information
            Assert.Equal("0.5887168", firstTrade.Fee);
            Assert.Equal("0.002", firstTrade.FeeRate);
            Assert.Equal("USDT", firstTrade.FeeCurrency);

            // Assert timestamp
            Assert.Equal(1758455614889, firstTrade.CreatedAt);

            // Assert empty stop field
            Assert.Equal("", firstTrade.Stop);
        }
    }
}