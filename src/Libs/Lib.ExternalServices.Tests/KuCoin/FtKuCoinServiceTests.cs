using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using Microsoft.Extensions.Options;

namespace Lib.ExternalServices.Tests.KuCoin
{
    public class FtKuCoinServiceTests : DependencyInjectionFixture
    {
        [Fact]
        public async Task GetOrders()
        {
            var config = GetService<IOptions<KuCoinConfig>>();
            var kuCoinConfig = config.Value;
            var kuCoinService = GetService<IFtKuCoinService>();

            var res = await kuCoinService.GetOrders(OrderStatus.Done, kuCoinConfig);
        }
    }
}