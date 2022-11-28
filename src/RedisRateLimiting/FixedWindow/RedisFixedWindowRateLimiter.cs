using RedisRateLimiting.Concurrency;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisFixedWindowRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisFixedWindowManager _redisManager;
        private readonly RedisFixedWindowRateLimiterOptions _options;

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisFixedWindowRateLimiter(TKey partitionKey, RedisFixedWindowRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.PermitLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.PermitLimit)), nameof(options));
            }
            if (options.Window <= TimeSpan.Zero)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than TimeSpan.Zero.", nameof(options.Window)), nameof(options));
            }
            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }

            _options = new RedisFixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
            };

            _redisManager = new RedisFixedWindowManager(partitionKey?.ToString() ?? string.Empty, _options);
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            return AcquireAsyncCoreInternal();
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var leaseContext = new FixedWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
            };

            var response = _redisManager.TryAcquireLease();

            leaseContext.Count = response.Count;
            leaseContext.RetryAfter = response.RetryAfter;

            if (leaseContext.Count > _options.PermitLimit)
            {
                return new FixedWindowLease(isAcquired: false, leaseContext);
            }

            return new FixedWindowLease(isAcquired: true, leaseContext);
        }

        private async ValueTask<RateLimitLease> AcquireAsyncCoreInternal()
        {
            var leaseContext = new FixedWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
            };

            var response = await _redisManager.TryAcquireLeaseAsync();

            leaseContext.Count = response.Count;
            leaseContext.RetryAfter = response.RetryAfter;

            if (leaseContext.Count > _options.PermitLimit)
            {
                return new FixedWindowLease(isAcquired: false, leaseContext);
            }

            return new FixedWindowLease(isAcquired: true, leaseContext);
        }

        private sealed class FixedWindowLeaseContext
        {
            public long Count { get; set; }

            public long Limit { get; set; }

            public TimeSpan Window { get; set; }

            public TimeSpan? RetryAfter { get; set; }
        }

        private sealed class FixedWindowLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name, RateLimitMetadataName.RetryAfter.Name };

            private readonly FixedWindowLeaseContext? _context;

            public FixedWindowLease(bool isAcquired, FixedWindowLeaseContext? context)
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
                    metadata = Math.Max(_context.Limit - _context.Count, 0);
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