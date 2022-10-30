using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RedisRateLimiting.Tests
{
    public class ConcurrencyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/concurrency";

        public ConcurrencyIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient(options: new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:7255")
            });
        }

        [Fact]
        public async Task GetRequestsEnforceLimit()
        {
            var tasks = new List<Task<int>>();

            for (var i = 0; i < 5; i++)
            {
                tasks.Add(MakeRequestAsync());
            }

            await Task.WhenAll(tasks);

            Assert.Equal(3, tasks.Count(x => x.Result == 429));

            await Task.Delay(1000);

            var statusCode = await MakeRequestAsync();
            Assert.Equal(200, statusCode);
        }

        private async Task<int> MakeRequestAsync()
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
            using var response = await _httpClient.SendAsync(request);
            return (int)response.StatusCode;
        }
    }
}