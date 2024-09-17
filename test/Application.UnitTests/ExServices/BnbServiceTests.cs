using Lib.ExternalServices.Bnd;
using Lib.ExternalServices.Bnd.Models;
using Refit;

namespace Application.UnitTests.ExServices
{
    public class BnbServiceTests
    {
        private IBndService _bndService;
        private const string API_KEY = "";
        private const string SECRET_KEY = "";
        public BnbServiceTests()
        {
            var httpClient = new HttpClient(new HttpDelegatingHandler()) { BaseAddress = new Uri("https://api.binance.com") };
            _bndService = RestService.For<IBndService>(httpClient);
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
            var res = await _bndService.GetTickerPrice("[\"BTCUSDT\",\"BNBUSDT\"]");
        }
    }
}
