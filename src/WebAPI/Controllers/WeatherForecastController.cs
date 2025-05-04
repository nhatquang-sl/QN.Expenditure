using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [OutputCache]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IDistributedCache _cache;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IDistributedCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            var cache = await _cache.GetStringAsync("WeatherForecast");
            if (string.IsNullOrWhiteSpace(cache))
            {
                var wfc = Enumerable
                    .Range(1, 5)
                    .Select(index => new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                    })
                    .ToArray();
                await _cache.SetStringAsync("WeatherForecast", JsonSerializer.Serialize(wfc));
                return wfc;
            }

            return JsonSerializer.Deserialize<WeatherForecast[]>(cache) ?? [];
        }
    }
}