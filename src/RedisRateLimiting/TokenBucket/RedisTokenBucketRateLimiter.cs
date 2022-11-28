using RedisRateLimiting.Concurrency;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisTokenBucketRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisTokenBucketManager _redisManager;
        private readonly RedisTokenBucketRateLimiterOptions _options;

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisTokenBucketRateLimiter(TKey partitionKey, RedisTokenBucketRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.TokenLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.TokenLimit)), nameof(options));
            }
            if (options.TokensPerPeriod <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.TokensPerPeriod)), nameof(options));
            }
            if (options.ReplenishmentPeriod <= TimeSpan.Zero)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than TimeSpan.Zero.", nameof(options.ReplenishmentPeriod)), nameof(options));
            }
            
            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }
            _options = new RedisTokenBucketRateLimiterOptions
            {
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
                TokenLimit = options.TokenLimit,
                ReplenishmentPeriod = options.ReplenishmentPeriod,
                TokensPerPeriod = options.TokensPerPeriod,
            };

            _redisManager = new RedisTokenBucketManager(partitionKey?.ToString() ?? string.Empty, _options);
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.TokenLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.TokenLimit));
            }

            return AcquireAsyncCoreInternal();
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.TokenLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.TokenLimit));
            }

            var leaseContext = new TokenBucketLeaseContext
            {
                Limit = _options.TokenLimit,
            };

            var response = _redisManager.TryAcquireLease();

            leaseContext.Allowed = response.Allowed;
            leaseContext.Count = response.Count;

            if (leaseContext.Allowed)
            {
                return new TokenBucketLease(isAcquired: true, leaseContext);
            }

            return new TokenBucketLease(isAcquired: false, leaseContext);
        }

        private async ValueTask<RateLimitLease> AcquireAsyncCoreInternal()
        {
            var leaseContext = new TokenBucketLeaseContext
            {
                Limit = _options.TokenLimit,
            };

            var response = await _redisManager.TryAcquireLeaseAsync();

            leaseContext.Allowed = response.Allowed;
            leaseContext.Count = response.Count;

            if (leaseContext.Allowed)
            {
                return new TokenBucketLease(isAcquired: true, leaseContext);
            }

            return new TokenBucketLease(isAcquired: false, leaseContext);
        }

        private sealed class TokenBucketLeaseContext
        {
            public long Count { get; set; }

            public long Limit { get; set; }

            public bool Allowed { get; set; }

            public TimeSpan? RetryAfter { get; set; }
        }

        private sealed class TokenBucketLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name, RateLimitMetadataName.RetryAfter.Name };

            private readonly TokenBucketLeaseContext? _context;

            public TokenBucketLease(bool isAcquired, TokenBucketLeaseContext? context)
            {
                IsAcquired = isAcquired;
                _context = context;
            }

            public override bool IsAcquired { get; }

            public override IEnumerable<string> MetadataNames => s_allMetadataNames;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (metadataName == RateLimitMetadataName.Limit.Name && _context is not null)
                {
                    metadata = _context.Limit.ToString();
                    return true;
                }

                if (metadataName == RateLimitMetadataName.Remaining.Name && _context is not null)
                {
                    metadata = _context.Count;
                    return true;
                }

                if (metadataName == RateLimitMetadataName.RetryAfter.Name && _context?.RetryAfter is not null)
                {
                    metadata = (int)_context.RetryAfter.Value.TotalSeconds;
                    return true;
                }

                metadata = default;
                return false;
            }
        }
    }
}