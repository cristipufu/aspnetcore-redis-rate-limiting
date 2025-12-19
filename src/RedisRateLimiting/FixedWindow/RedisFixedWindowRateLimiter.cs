using RedisRateLimiting.Concurrency;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisFixedWindowRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisFixedWindowManager _redisManager;
        private readonly RedisFixedWindowRateLimiterOptions _options;

        private readonly FixedWindowLease FailedLease = new(isAcquired: false, null);

        private int _activeRequestsCount;
        private long _idleSince = Stopwatch.GetTimestamp();

        public override TimeSpan? IdleDuration => Interlocked.CompareExchange(ref _activeRequestsCount, 0, 0) > 0
            ? null
            : Stopwatch.GetElapsedTime(_idleSince);

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

            return AcquireAsyncCoreInternal(permitCount);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            // https://github.com/cristipufu/aspnetcore-redis-rate-limiting/issues/66
            return FailedLease;
        }

        protected virtual void AddCustomMetadata(FixedWindowLeaseContext context) { }

        private async ValueTask<RateLimitLease> AcquireAsyncCoreInternal(int permitCount)
        {
            var leaseContext = new FixedWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
            };

            AddCustomMetadata(leaseContext);

            RedisFixedWindowResponse response;
            Interlocked.Increment(ref _activeRequestsCount);
            try
            {
                response = await _redisManager.TryAcquireLeaseAsync(permitCount);
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequestsCount);
                _idleSince = Stopwatch.GetTimestamp();
            }

            leaseContext.Count = response.Count;
            leaseContext.RetryAfter = response.RetryAfter;
            leaseContext.ExpiresAt = response.ExpiresAt;

            return new FixedWindowLease(isAcquired: response.Allowed, leaseContext);
        }

        protected sealed class FixedWindowLeaseContext
        {
            private Dictionary<string, object> _additionalMetadata = new();

            public long Count { get; set; }

            public long Limit { get; set; }

            public TimeSpan Window { get; set; }

            public void AddCustomMetadata(string name, object value)
            {
                _additionalMetadata[name] = value;
            }

            public bool TryGetCustomMetadata(string name, out object value)
            {
                return _additionalMetadata.TryGetValue(name, out value);
            }

            public IEnumerable<string> AdditionalMetadataNames => _additionalMetadata.Keys;

            public TimeSpan? RetryAfter { get; set; }

            public long? ExpiresAt { get; set; }
        }

        private sealed class FixedWindowLease : RateLimitLease
        {
            private readonly ISet<string> s_allMetadataNames;

            private readonly FixedWindowLeaseContext? _context;

            public FixedWindowLease(bool isAcquired, FixedWindowLeaseContext? context)
            {
                IsAcquired = isAcquired;
                _context = context;
                s_allMetadataNames = new HashSet<string>
                {
                    RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name,
                    RateLimitMetadataName.RetryAfter.Name
                };

                if (context?.AdditionalMetadataNames is not null)
                {
                    foreach (var name in context.AdditionalMetadataNames)
                    {
                        s_allMetadataNames.Add(name);
                    }
                }
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

                if (metadataName == RateLimitMetadataName.Reset.Name && _context?.ExpiresAt is not null)
                {
                    metadata = _context.ExpiresAt.Value;
                    return true;
                }

                if (_context != null && _context.TryGetCustomMetadata(metadataName, out var metadataValue))
                {
                    metadata = metadataValue;
                    return true;
                }

                metadata = default;
                return false;
            }
        }
    }
}