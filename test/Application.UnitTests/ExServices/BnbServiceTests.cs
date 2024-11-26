using Lib.ExternalServices.Bnb;
using Lib.ExternalServices.Bnb.Models;
using Refit;

namespace Application.UnitTests.ExServices
{
    public class BnbServiceTests
    {
        private IBnbService _bndService;
        private const string API_KEY = "FrsDrXFPNtuhTVExg6FEw0y8WuM11J4u7oFWovLudIUrtwISL1qDKJnYqYCtpdS2";
        private const string SECRET_KEY = "CDSNep2VUpgMgzuxzH92bVXlidsOitVEs4ZQd6BbtK2Qnlngaa1LvMk5aiPK4lRR";
        public BnbServiceTests()
        {
            var httpClient = new HttpClient(new HttpDelegatingHandler()) { BaseAddress = new Uri("https://api.binance.com") };
            _bndService = RestService.For<IBnbService>(httpClient);
        }

        [Fact]
        public async void Succeeds_QueryAllOrders()
        {
            var time = await _bndService.GetServerTime();
            var res = await _bndService.AllOrders(API_KEY, new AllOrdersRequest("IDUSDT", time.ServerTime, 0, SECRET_KEY));
        }

        [Fact]
        public async void Succeeds_GetTickerPrices()
        {
            var res = await _bndService.GetTickerPrice(["BTCUSDT", "BNBUSDT"]);
        }
    }
}
