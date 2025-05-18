using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[OutputCache]
    public class WeatherForecastController(ILogger<WeatherForecastController> logger) : ControllerBase
    {
        private static readonly string[] Summaries =
        [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];



        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            //var cache = await _cache.GetStringAsync("WeatherForecast");
            //if (string.IsNullOrWhiteSpace(cache))
            //{
            var wfc = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray();
            //await _cache.SetStringAsync("WeatherForecast", JsonSerializer.Serialize(wfc));
            //return wfc;
            //}

            return wfc;
        }
    }
}