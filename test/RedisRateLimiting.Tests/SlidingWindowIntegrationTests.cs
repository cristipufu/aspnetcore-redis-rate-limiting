using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RedisRateLimiting.Tests
{
    public class SlidingWindowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/slidingwindow";

        public SlidingWindowIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient(options: new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:7255")
            });
        }

        [Fact]
        public async Task GetRequestsEnforceLimit()
        {
            
        }
    }
}