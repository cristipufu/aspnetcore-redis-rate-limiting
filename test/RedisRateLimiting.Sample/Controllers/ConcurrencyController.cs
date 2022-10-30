using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RedisRateLimiting.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [EnableRateLimiting("demo_concurrency")]
    public class ConcurrencyController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public ConcurrencyController()
        {
        }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            await Task.Delay(1000);

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}