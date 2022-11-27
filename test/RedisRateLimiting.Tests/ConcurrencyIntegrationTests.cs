using Microsoft.AspNetCore.Mvc.Testing;
using RedisRateLimiting.AspNetCore;
using System.Net;
using Xunit;

namespace RedisRateLimiting.Tests
{
    [Collection("Seq")]
    public class ConcurrencyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiPath = "/concurrency";
        private readonly string _apiPathQueue = "/concurrency/queue";

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
            var tasks = new List<Task<RateLimitResponse>>();

            for (var i = 0; i < 5; i++)
            {
                tasks.Add(MakeRequestAsync(_apiPath));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(3, tasks.Count(x => x.Result.StatusCode == HttpStatusCode.TooManyRequests));
            Assert.Equal(3, tasks.Count(x => x.Result.Limit == 2));
            Assert.Equal(3, tasks.Count(x => x.Result.Remaining == 0));

            await Task.Delay(1000);

            var response = await MakeRequestAsync(_apiPath);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Find a way to send rate limit headers when request is successful as well
            Assert.Null(response.Limit); 
            Assert.Null(response.Remaining);
        }

        [Fact]
        public async Task GetQueuedRequests()
        {
            var tasks = new List<Task<RateLimitResponse>>();

            for (var i = 0; i < 5; i++)
            {
                tasks.Add(MakeRequestAsync(_apiPathQueue));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(5, tasks.Count(x => x.Result.StatusCode == HttpStatusCode.OK));
        }

        private async Task<RateLimitResponse> MakeRequestAsync(string path)
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"), path);
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