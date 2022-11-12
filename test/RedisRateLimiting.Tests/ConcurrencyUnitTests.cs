using Xunit;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedisRateLimiting.Tests
{
    public class ConcurrencyUnitTests
    {
        [Fact]
        public void InvalidOptionsThrows()
        {
            AssertExtensions.Throws<ArgumentNullException>("options", () => new RedisConcurrencyRateLimiter<string>(
                string.Empty,
                options: null));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisConcurrencyRateLimiter<string>(
                string.Empty,
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = -1,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisConcurrencyRateLimiter<string>(
                string.Empty,
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    ConnectionMultiplexerFactory = null,
                }));
        }

        [Fact]
        public async Task CanAcquireAsyncResource()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanAcquireAsyncResource",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    ConnectionMultiplexerFactory = GetRedisInstance
                });

            var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            var lease2 = await limiter.AcquireAsync();
            Assert.False(lease2.IsAcquired);

            lease.Dispose();

            lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            lease.Dispose();
        }

        [Fact]
        public void CanAcquireResource()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanAcquireResource",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    ConnectionMultiplexerFactory = GetRedisInstance
                });

            var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);

            var lease2 = limiter.AttemptAcquire();
            Assert.False(lease2.IsAcquired);

            lease.Dispose();
            lease2.Dispose();

            lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);

            lease.Dispose();
        }

        [Fact]
        public async Task CanAcquireResourceAsync_QueuesAndGrabsOldest()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanAcquireResourceAsync_QueuesAndGrabsOldest",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 2,
                    ConnectionMultiplexerFactory = GetRedisInstance,
                });

            var lease = await limiter.AcquireAsync();

            Assert.True(lease.IsAcquired);
            var wait1 = limiter.AcquireAsync();
            await Task.Delay(1000);
            var wait2 = limiter.AcquireAsync();
            Assert.False(wait1.IsCompleted);
            Assert.False(wait2.IsCompleted);

            lease.Dispose();

            lease = await wait1;
            Assert.True(lease.IsAcquired);

            lease.Dispose();

            lease = await wait2;
            Assert.True(lease.IsAcquired);

            lease.Dispose();
        }

        private IConnectionMultiplexer GetRedisInstance()
        {
            var redisOptions = ConfigurationOptions.Parse(",ssl=True,abortConnect=False");
            return ConnectionMultiplexer.Connect(redisOptions);
        }

    }
}
