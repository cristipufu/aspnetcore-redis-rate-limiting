using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisSlidingWindowRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisSlidingWindowRateLimiterOptions _options;
        private readonly TKey _partitionKey;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly LuaScript _redisScript = LuaScript.Prepare(
            @"
            local limit = tonumber(@permit_limit)
            local window = tonumber(@window)
            local window_ms = window * 1000

            local now = redis.call('TIME')
            local now_microsec = now[1] * 1000000 + now[2] -- Timestamp in microseconds
            local now_ms = math.floor(now_microsec * 0.001) -- Timestamp in milliseconds
            local window_expire_at = now_ms + window_ms -- Window expiration epoch in milliseconds

            -- remove elements older than the current windows (now - window)
            redis.call('ZREMRANGEBYSCORE', @rate_limit_key, 0, now_ms - window_ms)

            -- get number of requests in current window
            local current_count = redis.call('ZCARD', @rate_limit_key)

            -- compute the number of remaining allowed requests
            local remaining_allowed = limit - current_count

            -- compute if request is allowed
            local allowed = current_count < limit

            local first_expire_at = -1

            if current_count > 0 then
                -- get first element (microseconds)
                local first_member = redis.call('ZRANGE', @rate_limit_key, 0, 0)[1]
                -- convert to miliseconds and add expire time
                first_expire_at = math.floor(first_member * 0.001) + window_ms
            else
                first_expire_at = window_expire_at
            end

            if allowed then
                redis.call('ZADD', @rate_limit_key, now_ms, now_microsec)
            end

            redis.call('EXPIRE', @rate_limit_key, window)

            return { allowed, remaining_allowed - 1, first_expire_at, window_expire_at }
            ");

        private static readonly RateLimitLease FailedLease = new SlidingWindowLease(false, null);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisSlidingWindowRateLimiter(TKey partitionKey, RedisSlidingWindowRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.PermitLimit <= 0)
            {
                throw new ArgumentException(
                    $"{nameof(options.PermitLimit)} must be set to a value greater than 0.",
                    nameof(options));
            }

            if (options.Window <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    $"{nameof(options.Window)} must be set to a value greater than TimeSpan.Zero.",
                    nameof(options));
            }

            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(
                    $"{nameof(options.ConnectionMultiplexerFactory)} must not be null.",
                    nameof(options));
            }

            _options = new RedisSlidingWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
            };

            _partitionKey = partitionKey;

            _connectionMultiplexer = _options.ConnectionMultiplexerFactory();
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount,
            CancellationToken cancellationToken)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount,
                    $"{permitCount} permit(s) exceeds the permit limit of {_options.PermitLimit}.");
            }

            var leaseContext = new SlidingWindowLeaseContext
            {
                Limit = _options.PermitLimit,
                Window = _options.Window,
                Remaining = _options.PermitLimit,
            };

            var database = _connectionMultiplexer.GetDatabase();

            var now = DateTimeOffset.UtcNow;

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = $"rl:{_partitionKey}",
                    window = _options.Window.Seconds,
                    permit_limit = _options.PermitLimit,
                });

            var allowed = false;

            if (response is not null)
            {
                allowed = (bool)response[0];

                leaseContext.Remaining = Math.Max(0, (long)response[1]);
                leaseContext.RetryAfter = TimeSpan.FromMilliseconds((long)response[2] - now.ToUnixTimeMilliseconds());
            }

            return new SlidingWindowLease(isAcquired: allowed, leaseContext);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            return FailedLease;
        }

        private sealed class SlidingWindowLeaseContext
        {
            public long Remaining { get; set; }

            public long Limit { get; set; }

            public TimeSpan Window { get; set; }

            public TimeSpan? RetryAfter { get; set; }
        }

        private sealed class SlidingWindowLease : RateLimitLease
        {
            private static readonly string[] AllMetadataNames =
            {
                RateLimitMetadataName.Limit.Name,
                RateLimitMetadataName.Remaining.Name,
                RateLimitMetadataName.RetryAfter.Name
            };

            private readonly SlidingWindowLeaseContext? _context;

            public SlidingWindowLease(bool isAcquired, SlidingWindowLeaseContext? context)
            {
                IsAcquired = isAcquired;
                _context = context;
            }

            public override bool IsAcquired { get; }

            public override IEnumerable<string> MetadataNames => AllMetadataNames;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (metadataName == RateLimitMetadataName.Limit.Name && _context is not null)
                {
                    metadata = _context?.Limit.ToString();
                    return true;
                }

                if (metadataName == RateLimitMetadataName.Remaining.Name && _context is not null)
                {
                    metadata = _context?.Remaining;
                    return true;
                }

                if (metadataName == RateLimitMetadataName.RetryAfter.Name && _context is not null)
                {
                    metadata = (int)_context?.RetryAfter?.TotalSeconds;
                    return true;
                }

                metadata = default;
                return false;
            }
        }
    }
}