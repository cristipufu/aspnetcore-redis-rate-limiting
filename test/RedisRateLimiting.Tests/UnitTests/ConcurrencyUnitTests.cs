using Xunit;

namespace RedisRateLimiting.Tests.UnitTests
{
    public class ConcurrencyUnitTests : IClassFixture<TestFixture>
    {
        private readonly TestFixture Fixture;

        public ConcurrencyUnitTests(TestFixture fixture)
        {
            Fixture = fixture;
        }

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
                    QueueLimit = -1,
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
        public async Task ThrowsWhenAcquiringMoreThanLimit()
        {
            var limiter = new RedisConcurrencyRateLimiter<string>(
                string.Empty,
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
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
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanAcquireAsyncResource",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
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
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
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
        public async Task CanAcquireResourceAsyncQueuesAndGrabsOldest()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanAcquireResourceAsyncQueuesAndGrabsOldest",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 2,
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
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

        [Fact]
        public async Task FailsWhenQueuingMoreThanLimit()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_FailsWhenQueuingMoreThanLimit",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            using var lease = limiter.AttemptAcquire();
            var wait = limiter.AcquireAsync();

            var failedLease = await limiter.AcquireAsync();
            Assert.False(failedLease.IsAcquired);
        }

        [Fact]
        public async Task QueueAvailableAfterQueueLimitHitAndResourcesBecomeAvailable()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_QueueAvailableAfterQueueLimitHitAndResourcesBecomeAvailable",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            var lease = limiter.AttemptAcquire();
            var wait = limiter.AcquireAsync();

            var failedLease = await limiter.AcquireAsync();
            Assert.False(failedLease.IsAcquired);

            lease.Dispose();
            lease = await wait;
            Assert.True(lease.IsAcquired);

            wait = limiter.AcquireAsync();
            Assert.False(wait.IsCompleted);

            lease.Dispose();
            lease = await wait;
            Assert.True(lease.IsAcquired);
            lease.Dispose();
        }

        [Fact]
        public async Task CanDequeueMultipleResourcesAtOnce()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "Test_CanDequeueMultipleResourcesAtOnce",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 2,
                    QueueLimit = 2,
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });
            using var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);
            using var leaseNew = await limiter.AcquireAsync();
            Assert.True(leaseNew.IsAcquired);

            var wait1 = limiter.AcquireAsync();
            var wait2 = limiter.AcquireAsync();
            Assert.False(wait1.IsCompleted);
            Assert.False(wait2.IsCompleted);

            lease.Dispose();
            leaseNew.Dispose();

            using var lease1 = await wait1;
            using var lease2 = await wait2;
            Assert.True(lease1.IsAcquired);
            Assert.True(lease2.IsAcquired);
        }
    }
}
