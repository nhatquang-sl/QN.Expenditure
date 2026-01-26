using System.Text.Json;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController(
        HybridCache cache,
        IDistributedCache distributedCache,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig) : ControllerBase
    {
        //private HybridCache _cache = cache;
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        [HttpGet("output-cache")]
        [OutputCache]
        public async Task<IEnumerable<Kline>> Get(CancellationToken token = default)
        {
            const IntervalType interval = IntervalType.OneDay;
            var startDate = interval.GetStartDate();
            return await kuCoinService.GetKlines("BTCUSDT", interval, startDate,
                DateTime.UtcNow, kuCoinConfig.Value);
        }

        [HttpGet("hybrid-cache")]
        public async Task<IEnumerable<Kline>> GetWithHybridCache(CancellationToken token = default)
        {
            return await cache.GetOrCreateAsync("WeatherForecastHybridCache", async cancel =>
            {
                const IntervalType interval = IntervalType.OneDay;
                var startDate = interval.GetStartDate();
                return await kuCoinService.GetKlines("BTCUSDT", interval, startDate,
                    DateTime.UtcNow, kuCoinConfig.Value);
            }, cancellationToken: token);
        }

        [HttpGet("distributed-cache")]
        public async Task<IEnumerable<Kline>> GetWithDistributedCache(CancellationToken token = default)
        {
            var cacheValue = await distributedCache.GetAsync("WeatherForecastDistributedCache", token);
            List<Kline> candles;
            if (cacheValue != null)
            {
                // Deserialize the cached value
                candles = JsonSerializer.Deserialize<List<Kline>>(cacheValue) ?? [];
                return candles;
            }

            const IntervalType interval = IntervalType.OneDay;
            var startDate = interval.GetStartDate();
            candles = await kuCoinService.GetKlines("BTCUSDT", interval, startDate,
                DateTime.UtcNow, kuCoinConfig.Value);
            await distributedCache.SetStringAsync("WeatherForecastDistributedCache", JsonSerializer.Serialize(candles),
                token);

            return candles;
        }
    }
}