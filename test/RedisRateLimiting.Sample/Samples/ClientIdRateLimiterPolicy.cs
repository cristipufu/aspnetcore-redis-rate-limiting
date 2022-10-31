using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedisRateLimiting.Sample.Samples
{
    public class ClientIdRateLimiterPolicy : IRateLimiterPolicy<string>
    {
        private readonly Func<OnRejectedContext, CancellationToken, ValueTask>? _onRejected;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public ClientIdRateLimiterPolicy(
            IConnectionMultiplexer connectionMultiplexer,
            ILogger<ClientIdRateLimiterPolicy> logger)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _onRejected = (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                logger.LogInformation($"Request rejected by {nameof(ClientIdRateLimiterPolicy)}");
                return ValueTask.CompletedTask;
            };
        }

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get => _onRejected; }

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            return RedisRateLimitPartition.GetRedisFixedWindowRateLimiter(string.Empty, key => new RedisFixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                ConnectionMultiplexerFactory = () => _connectionMultiplexer,
                Window = TimeSpan.FromSeconds(5),
            });
        }
    }

}
