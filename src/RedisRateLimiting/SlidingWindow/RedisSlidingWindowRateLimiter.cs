using RedisRateLimiting.Concurrency;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisSlidingWindowRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisSlidingWindowManager _redisManager;
        private readonly RedisSlidingWindowRateLimiterOptions _options;
        
        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisSlidingWindowRateLimiter(TKey partitionKey, RedisSlidingWindowRateLimiterOptions options)
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

            _options = new RedisSlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
            };

            _redisManager = new RedisSlidingWindowManager(partitionKey?.ToString() ?? string.Empty, _options);
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var leaseContext = new SlidingWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
                RequestId = Guid.NewGuid().ToString(),
            };

            var response = await _redisManager.TryAcquireLeaseAsync(leaseContext.RequestId);

            leaseContext.Count = response.Count;
            leaseContext.Allowed = response.Allowed;

            if (leaseContext.Allowed)
            {
                return new SlidingWindowLease(isAcquired: true, leaseContext);
            }

            return new SlidingWindowLease(isAcquired: false, leaseContext);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var leaseContext = new SlidingWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
                RequestId = Guid.NewGuid().ToString(),
            };

            var response = _redisManager.TryAcquireLease(leaseContext.RequestId);

            leaseContext.Count = response.Count;
            leaseContext.Allowed = response.Allowed;

            if (leaseContext.Allowed)
            {
                return new SlidingWindowLease(isAcquired: true, leaseContext);
            }

            return new SlidingWindowLease(isAcquired: false, leaseContext);
        }

        private sealed class SlidingWindowLeaseContext
        {
            public long Count { get; set; }

            public long Limit { get; set; }

            public TimeSpan Window { get; set; }

            public bool Allowed { get; set; }

            public string? RequestId { get; set; }
        }

        private sealed class SlidingWindowLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name };

            private readonly SlidingWindowLeaseContext? _context;

            public SlidingWindowLease(bool isAcquired, SlidingWindowLeaseContext? context)
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

                metadata = default;
                return false;
            }
        }
    }
}