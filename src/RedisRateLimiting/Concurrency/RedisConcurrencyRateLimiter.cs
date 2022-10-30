using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisConcurrencyRateLimiter : RateLimiter
    {
        private readonly RedisConcurrencyRateLimiterOptions _options;
        private readonly string _policyName;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly ConcurrencyLease FailedLease = new(false, null, 0);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisConcurrencyRateLimiter(string policyName, RedisConcurrencyRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.PermitLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.PermitLimit)), nameof(options));
            }
            if (options.ConnectionMultiplexer is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexer)), nameof(options));
            }

            _policyName = policyName;

            _options = new RedisConcurrencyRateLimiterOptions
            {
                ConnectionMultiplexer = options.ConnectionMultiplexer,
                PermitLimit = options.PermitLimit,
            };

            _connectionMultiplexer = _options.ConnectionMultiplexer;
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            return new ValueTask<RateLimitLease>(FailedLease);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            if (true)
            {
                return new ConcurrencyLease(isAcquired: true, this, permitCount);
            }
            else
            {
                return FailedLease;
            }
        }

        private void Release(int releaseCount)
        {
            //_count--;
        }

        private sealed class ConcurrencyLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { MetadataName.ReasonPhrase.Name };

            private bool _disposed;
            private readonly RedisConcurrencyRateLimiter? _limiter;
            private readonly int _count;
            private readonly string? _reason;

            public ConcurrencyLease(bool isAcquired, RedisConcurrencyRateLimiter? limiter, int count, string? reason = null)
            {
                IsAcquired = isAcquired;
                _limiter = limiter;
                _count = count;
                _reason = reason;
            }

            public override bool IsAcquired { get; }

            public override IEnumerable<string> MetadataNames => s_allMetadataNames;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (_reason is not null && metadataName == MetadataName.ReasonPhrase.Name)
                {
                    metadata = _reason;
                    return true;
                }
                metadata = default;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                _limiter?.Release(_count);
            }
        }
    }
}
