using Microsoft.AspNetCore.Mvc.Testing;
using RedisRateLimiting.AspNetCore;
using System.Net;
using Xunit;

namespace RedisRateLimiting.Tests
{
    public class TokenBucketIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/tokenbucket";

        public TokenBucketIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient(options: new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:7255")
            });
        }

        [Fact]
        public async Task GetRequestsEnforceLimit()
        {
            var response = await MakeRequestAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await MakeRequestAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            response = await MakeRequestAsync();
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(2, response.Limit);
            Assert.Equal(0, response.Remaining);
            //Assert.NotNull(response.RetryAfter);

            await Task.Delay(1000 * 2);

            response = await MakeRequestAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<RateLimitResponse> MakeRequestAsync()
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), _apiPath);
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

            if (response.Headers.TryGetValues(RateLimitHeaders.RetryAfter, out var valuesRetryAfter)
                && int.TryParse(valuesRetryAfter.FirstOrDefault(), out var retryAfter))
            {
                rateLimitResponse.RetryAfter = retryAfter;
            }

            return rateLimitResponse;
        }

        private sealed class RateLimitResponse
        {
            public HttpStatusCode StatusCode { get; set; }

            public long? Limit { get; set; }

            public long? Remaining { get; set; }

            public int? RetryAfter { get; set; }
        }
    }
}