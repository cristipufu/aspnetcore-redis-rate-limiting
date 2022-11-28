using Xunit;

namespace RedisRateLimiting.Tests.UnitTests
{
    public class TokenBucketUnitTests : IClassFixture<TestFixture>
    {
        private readonly TestFixture Fixture;

        public TokenBucketUnitTests(TestFixture fixture)
        {
            Fixture = fixture;
        }

        [Fact]
        public void InvalidOptionsThrows()
        {
            AssertExtensions.Throws<ArgumentNullException>("options", () => new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                options: null));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = -1,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
               string.Empty,
               new RedisTokenBucketRateLimiterOptions
               {
                   TokenLimit = 1,
                   TokensPerPeriod = -1,
               }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
               string.Empty,
               new RedisTokenBucketRateLimiterOptions
               {
                   TokenLimit = 1,
                   TokensPerPeriod = 1,
               }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.Zero,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.Zero,
                }));

            AssertExtensions.Throws<ArgumentException>("options", () => new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = null,
                }));
        }

        [Fact]
        public async Task ThrowsWhenAcquiringMoreThanLimit()
        {
            var limiter = new RedisTokenBucketRateLimiter<string>(
                string.Empty,
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
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
            using var limiter = new RedisTokenBucketRateLimiter<string>(
                "Test_CanAcquireAsyncResource_TB",
                new RedisTokenBucketRateLimiterOptions
                {
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            using var lease2 = await limiter.AcquireAsync();
            Assert.False(lease2.IsAcquired);
        }
    }
}
