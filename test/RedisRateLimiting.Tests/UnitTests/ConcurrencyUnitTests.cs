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
                    PermitLimit = 1,
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

            var wait2 = limiter.AcquireAsync();
            Assert.False(wait2.IsCompleted);

            lease.Dispose();
            lease = await wait2;
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

            await Task.Delay(TimeSpan.FromSeconds(1));

            lease.Dispose();
            leaseNew.Dispose();

            using var lease1 = await wait1;
            using var lease2 = await wait2;
            Assert.True(lease1.IsAcquired);
            Assert.True(lease2.IsAcquired);
        }

        [Fact]
        public async Task CanCancelAcquireAsyncAfterQueuing()
        {
            var limiter = new RedisConcurrencyRateLimiter<string>(
                "CanCancelAcquireAsyncAfterQueuing",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
                    TryDequeuePeriod = TimeSpan.FromHours(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });
            var lease = await limiter.AcquireAsync();
            Assert.True(lease.IsAcquired);

            var cts = new CancellationTokenSource();

            var waitQueued = limiter.AcquireAsync(cancellationToken: cts.Token);
            Assert.False(waitQueued.IsCompleted);
            await Task.Delay(TimeSpan.FromSeconds(1));

            lease.Dispose();

            // cancel request while queued
            cts.Cancel();

            ForceDequeue(limiter);

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(waitQueued.AsTask);
            Assert.Equal(cts.Token, ex.CancellationToken);

            // the queue should be empty now
            var lease2 = await limiter.AcquireAsync();
            Assert.True(lease2.IsAcquired);

            var waitQueued2 = limiter.AcquireAsync();
            Assert.False(waitQueued2.IsCompleted);
            await Task.Delay(TimeSpan.FromSeconds(1));

            lease2.Dispose();

            ForceDequeue(limiter);

            using var leaseQueued = await waitQueued2;
            Assert.True(leaseQueued.IsAcquired);

            await limiter.DisposeAsync();
            limiter.Dispose();
        }

        [Fact]
        public async Task GetPermitWhilePermitEmptyQueueNotEmptyGetsQueued()
        {
            using var limiter = new RedisConcurrencyRateLimiter<string>(
                "GetPermitWhilePermitEmptyQueueNotEmptyGetsQueued",
                new RedisConcurrencyRateLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 1,
                    TryDequeuePeriod = TimeSpan.FromHours(1),
                    ConnectionMultiplexerFactory = Fixture.ConnectionMultiplexerFactory,
                });

            // t1. req1 => 1 permit
            var lease1 = await limiter.AcquireAsync();
            Assert.True(lease1.IsAcquired);

            //// t2. req2 => 1 queue
            var wait2 = limiter.AcquireAsync();
            Assert.False(wait2.IsCompleted);

            await Task.Delay(TimeSpan.FromSeconds(1));

            // t3. req1 finish => 0 permit (0 permit, 1 queue)
            lease1.Dispose();

            // t4. req3 => 2 queue (0 permit, 2 queue)
            var wait3 = limiter.AcquireAsync();
            Assert.False(wait3.IsCompleted);

            await Task.Delay(TimeSpan.FromSeconds(1));

            // t5. req4 => rejected
            // (permit + queue) would be gt (permit_limit + queue_limit)
            using var lease4 = await limiter.AcquireAsync();
            Assert.False(lease4.IsAcquired);

            // t6. dequeue requests
            ForceDequeue(limiter);

            var lease2 = await wait2;
            Assert.True(lease2.IsAcquired);

            lease2.Dispose();

            using var lease3 = await wait3;
            Assert.True(lease3.IsAcquired);
        }

        static internal void ForceDequeue(RedisConcurrencyRateLimiter<string> limiter)
        {
            var dequeueRequestsMethod = typeof(RedisConcurrencyRateLimiter<string>)
                .GetMethod("TryDequeueRequestsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            dequeueRequestsMethod.Invoke(limiter, Array.Empty<object>());
        }
    }
}