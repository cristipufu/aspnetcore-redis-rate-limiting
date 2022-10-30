using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RedisRateLimiting.Tests
{
    public class FixedWindowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/fixedwindow";

        public FixedWindowIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient(options: new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:7255")
            });
        }

        [Fact]
        public async Task GetRequestsEnforceLimit()
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
            using var response = await _httpClient.SendAsync(request);
            int responseStatusCode = (int)response.StatusCode;
            await response.Content.ReadAsStringAsync();
            Assert.Equal(200, responseStatusCode);

            using var request2 = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
            using var response2 = await _httpClient.SendAsync(request2);
            responseStatusCode = (int)response2.StatusCode;
            await response2.Content.ReadAsStringAsync();
            Assert.Equal(429, responseStatusCode);

            await Task.Delay(1000 * 2);

            using var request3 = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
            using var response3 = await _httpClient.SendAsync(request3);
            responseStatusCode = (int)response3.StatusCode;
            await response3.Content.ReadAsStringAsync();
            Assert.Equal(200, responseStatusCode);
        }

    }
}