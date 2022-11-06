using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisConcurrencyRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisConcurrencyRateLimiterOptions _options;
        private readonly TKey _partitionKey;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly LuaScript _redisScript = LuaScript.Prepare(
          @"local limit = tonumber(@permit_limit)
            local timestamp = tonumber(@current_time)
            local ttl = 60

            redis.call(""zremrangebyscore"", @rate_limit_key, '-inf', timestamp - ttl)
            local count = redis.call(""zcard"", @rate_limit_key)
            local allowed = count < limit

            if allowed then
                redis.call(""zadd"", @rate_limit_key, timestamp, @unique_id)
            end

            return { allowed, count }");

        private static readonly ConcurrencyLease FailedLease = new(false, null, null);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisConcurrencyRateLimiter(TKey partitionKey, RedisConcurrencyRateLimiterOptions options)
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

            _partitionKey = partitionKey;

            _options = new RedisConcurrencyRateLimiterOptions
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

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var leaseContext = new ConcurencyLeaseContext
            {
                Limit = _options.PermitLimit,
                RequestId = Guid.NewGuid().ToString(),
            };

            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    permit_limit = _options.PermitLimit,
                    rate_limit_key = $"rl:{_partitionKey}",
                    current_time = nowUnixTimeSeconds,
                    unique_id = leaseContext.RequestId,
                });

            bool allowed = false;

            if (response != null)
            {
                allowed = (bool)response[0];
                leaseContext.Count = (long)response[1];
            }

            if (allowed)
            {
                return new ConcurrencyLease(isAcquired: true, this, leaseContext);
            }

            return new ConcurrencyLease(isAcquired: false, this, leaseContext);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            return FailedLease;
        }

        private void Release(ConcurencyLeaseContext leaseContext)
        {
            var database = _connectionMultiplexer.GetDatabase();
            // how to use async? if only RateLimitLease would implement IAsyncDisposable
            database.SortedSetRemove($"rl:{_partitionKey}", leaseContext.RequestId);
        }

        private sealed class ConcurencyLeaseContext
        {
            public string? RequestId { get; set; }

            public long Count { get; set; }

            public long Limit { get; set; }
        }

        private sealed class ConcurrencyLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { MetadataName.ReasonPhrase.Name, RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name };

            private bool _disposed;
            private readonly RedisConcurrencyRateLimiter<TKey>? _limiter;
            private readonly ConcurencyLeaseContext? _context;

            public ConcurrencyLease(bool isAcquired, RedisConcurrencyRateLimiter<TKey>? limiter, ConcurencyLeaseContext? context)
            {
                IsAcquired = isAcquired;
                _limiter = limiter;
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
                    metadata = _context.Limit - _context.Count;
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

                if (_context != null)
                {
                    _limiter?.Release(_context);
                }
            }
        }
    }
}
