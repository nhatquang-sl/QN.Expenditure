using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HybridCacheController(HybridCache hybridCache) : ControllerBase
    {
        [HttpGet("{key}")]
        public async Task<string> Get(string key, CancellationToken token)
        {
            var result = await hybridCache.GetOrCreateAsync($"key: {key}", async ct =>
            {
                await Task.Delay(1000, ct);
                return $"Value for {key} at {DateTime.UtcNow}";
            }, tags: ["hc-tag"], cancellationToken: token);

            return result;
        }

        [HttpDelete("{key}")]
        public async Task Delete(string key, CancellationToken token)
        {
            await hybridCache.RemoveAsync($"key: {key}", token);
        }

        [HttpDelete("tags/{tags}")]
        public async Task DeleteByTags(string tags, CancellationToken token)
        {
            await hybridCache.RemoveByTagAsync(tags.Split(","), token);
        }
    }
}