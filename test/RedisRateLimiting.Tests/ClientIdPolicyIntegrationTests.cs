using Microsoft.AspNetCore.Mvc.Testing;
using RedisRateLimiting.AspNetCore;
using RedisRateLimiting.Sample;
using System.Net;
using Xunit;

namespace RedisRateLimiting.Tests
{
    public class ClientIdPolicyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/clients";

        public ClientIdPolicyIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient(options: new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:7255"),
            });
        }

        [Theory]
        [InlineData("client1", 1)]
        [InlineData("client2", 2)]
        [InlineData("client3", 3)]
        [InlineData("clientX", 1)]
        public async Task GetRequestsEnforceLimit(string clientId, int permitLimit)
        {
            var tasks = new List<Task<RateLimitResponse>>();
            var extra = 2;

            for (var i = 0; i < permitLimit + extra; i++)
            {
                tasks.Add(MakeRequestAsync(clientId));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(permitLimit, tasks.Count(x => x.Result.StatusCode == HttpStatusCode.OK));
            Assert.Equal(extra, tasks.Count(x => x.Result.StatusCode == HttpStatusCode.TooManyRequests));
        }

        private async Task<RateLimitResponse> MakeRequestAsync(string clientId)
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
            request.Headers.Add("X-ClientId", clientId);

            using var response = await _httpClient.SendAsync(request);

            var rateLimitResponse = new RateLimitResponse
            {
                StatusCode = response.StatusCode,
            };

            if (response.Headers.TryGetValues(RateLimitHeaders.Limit, out var valuesLimit)
                && long.TryParse(valuesLimit.FirstOrDefault(), out var limit))
            {
                rateLimitResponse.Limit = limit;
            }

            if (response.Headers.TryGetValues(RateLimitHeaders.Remaining, out var valuesRemaining)
                && long.TryParse(valuesRemaining.FirstOrDefault(), out var remaining))
            {
                rateLimitResponse.Remaining = remaining;
            }

            return rateLimitResponse;
        }

        private sealed class RateLimitResponse
        {
            public HttpStatusCode StatusCode { get; set; }

            public long? Limit { get; set; }

            public long? Remaining { get; set; }
        }
    }
}