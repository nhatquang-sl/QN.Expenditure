using System.Text.Json;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CandlesController(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        HybridCache cache,
        IDistributedCache distributedCache)
        : ControllerBase
    {
        [HttpGet("{symbol}/{interval}")]
        public async Task<List<Kline>> Get(string symbol, IntervalType interval,
            CancellationToken cancellationToken = default)
        {
            var cacheValue = await distributedCache.GetAsync($"candles_{symbol}_{interval}", cancellationToken);
            List<Kline> candles;
            if (cacheValue != null)
            {
                // Deserialize the cached value
                candles = JsonSerializer.Deserialize<List<Kline>>(cacheValue) ?? [];
                return candles;
            }

            var startDate = interval.GetStartDate();
            candles = await kuCoinService.GetKlines(symbol, interval, startDate,
                DateTime.UtcNow, kuCoinConfig.Value);
            await distributedCache.SetStringAsync($"candles_{symbol}_{interval}", JsonSerializer.Serialize(candles),
                cancellationToken);
            // var candles = await cache.GetOrCreateAsync(
            //     $"candles_{symbol}_{interval}",
            //     async cancel =>
            //     {
            //         var startDate = interval.GetStartDate();
            //         return await kuCoinService.GetKlines(symbol, interval.GetDescription(), startDate,
            //             DateTime.UtcNow, kuCoinConfig.Value);
            //     },
            //     cancellationToken: cancellationToken
            // );
            //
            return candles;
        }
    }
}