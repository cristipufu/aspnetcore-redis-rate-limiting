using Xunit;

namespace RedisRateLimiting.Tests.UnitTests
{
    public class SlidingWindowUnitTests(TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly TestFixture Fixture = fixture;

        [Fact]
        public void InvalidOptionsThrows()
        {
            AssertExtensions.Throws<ArgumentNullException>("options", () => new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                options: null));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = -1,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisSlidingWindowRateLimiter<string>(
               string.Empty,
               new RedisSlidingWindowRateLimiterOptions
               {
                   PermitLimit = 1,
               }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.Zero,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(-1),
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = null,
                }));
        }

        [Fact]
        public async Task ThrowsWhenAcquiringMoreThanLimit()
        {
            var limiter = new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AttemptAcquire(2));
            Assert.Equal("permitCount", ex.ParamName);
            ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await limiter.AcquireAsync(2));
            Assert.Equal("permitCount", ex.ParamName);
        }

        [Fact]
        public async Task CanAcquireAsyncResource()
        {
            await Fixture.ClearStatisticsAsync("Test_CanAcquireAsyncResource_SW");

            using var limiter = new RedisSlidingWindowRateLimiter<string>(
                "Test_CanAcquireAsyncResource_SW",
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            using var lease2 = await limiter.AcquireAsync();
            Assert.False(lease2.IsAcquired);

            var stats = limiter.GetStatistics()!;
            Assert.Equal(1, stats.TotalSuccessfulLeases);
            Assert.Equal(1, stats.TotalFailedLeases);
            Assert.Equal(0, stats.CurrentAvailablePermits);

            lease.Dispose();
            lease2.Dispose();

            stats = limiter.GetStatistics()!;
            Assert.Equal(0, stats.CurrentAvailablePermits);
        }

        [Fact]
        public async Task CanAcquireAsyncResourceWithSmallWindow()
        {
            using var limiter = new RedisSlidingWindowRateLimiter<string>(
                string.Empty,
                new RedisSlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMilliseconds(100),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            using var lease2 = await limiter.AcquireAsync();
            Assert.False(lease2.IsAcquired);

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            using var lease3 = await limiter.AcquireAsync();
            Assert.True(lease3.IsAcquired);

            using var lease4 = await limiter.AcquireAsync();
            Assert.False(lease4.IsAcquired);
        }
    }
}
