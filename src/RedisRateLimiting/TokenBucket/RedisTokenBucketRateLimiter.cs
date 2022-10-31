using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisTokenBucketRateLimiter : RateLimiter
    {
        private readonly RedisTokenBucketRateLimiterOptions _options;
        private readonly string _policyName;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly TokenBucketLease FailedLease = new(false, null, 0);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisTokenBucketRateLimiter(string policyName, RedisTokenBucketRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.PermitLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.PermitLimit)), nameof(options));
            }
            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }

            _policyName = policyName;

            _options = new RedisTokenBucketRateLimiterOptions
            {
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
                PermitLimit = options.PermitLimit,
            };

            _connectionMultiplexer = _options.ConnectionMultiplexerFactory();
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
            return FailedLease;
        }

        private void Release(int releaseCount)
        {
            //_count--;
        }

        private sealed class TokenBucketLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { MetadataName.ReasonPhrase.Name };

            private bool _disposed;
            private readonly RedisTokenBucketRateLimiter? _limiter;
            private readonly int _count;
            private readonly string? _reason;

            public TokenBucketLease(bool isAcquired, RedisTokenBucketRateLimiter? limiter, int count, string? reason = null)
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
