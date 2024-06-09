using Xunit;

namespace RedisRateLimiting.Tests.UnitTests
{
    public class FixedWindowUnitTests(TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly TestFixture Fixture = fixture;

        [Fact]
        public void InvalidOptionsThrows()
        {
            AssertExtensions.Throws<ArgumentNullException>("options", () => new RedisFixedWindowRateLimiter<string>(
                string.Empty,
                options: null));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisFixedWindowRateLimiter<string>(
                string.Empty,
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = -1,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisFixedWindowRateLimiter<string>(
                string.Empty,
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.Zero,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisFixedWindowRateLimiter<string>(
                string.Empty,
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(-1),
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisFixedWindowRateLimiter<string>(
                string.Empty,
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = null,
                }));
        }

        [Fact]
        public async Task ThrowsWhenAcquiringMoreThanLimit()
        {
            var limiter = new RedisFixedWindowRateLimiter<string>(
                partitionKey: Guid.NewGuid().ToString(),
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });
            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await limiter.AcquireAsync(2));
            Assert.Equal("permitCount", ex.ParamName);
        }

        [Fact]
        public async Task CanAcquireAsyncResource()
        {
            using var limiter = new RedisFixedWindowRateLimiter<string>(
                partitionKey: Guid.NewGuid().ToString(),
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            using var lease2 = await limiter.AcquireAsync();
            Assert.False(lease2.IsAcquired);
        }

        [Fact]
        public async Task CanAcquireMultiplePermits()
        {
            using var limiter = new RedisFixedWindowRateLimiter<string>(
                partitionKey: Guid.NewGuid().ToString(),
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = await limiter.AcquireAsync(permitCount: 3);
            Assert.True(lease.IsAcquired);

            using var lease2 = await limiter.AcquireAsync(permitCount: 3);
            Assert.False(lease2.IsAcquired);

            using var lease3 = await limiter.AcquireAsync(permitCount: 2);
            Assert.True(lease3.IsAcquired);
        }
        
        [Fact]
        public async Task IdleDurationIsUpdated()
        {
            await using var limiter = new RedisFixedWindowRateLimiter<string>(
                partitionKey: Guid.NewGuid().ToString(),
                new RedisFixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });
            await Task.Delay(TimeSpan.FromMilliseconds(5));
            Assert.NotEqual(TimeSpan.Zero, limiter.IdleDuration);

            var previousIdleDuration = limiter.IdleDuration;
            using var lease = await limiter.AcquireAsync();
            Assert.True(limiter.IdleDuration < previousIdleDuration);
        }        
    }
}
