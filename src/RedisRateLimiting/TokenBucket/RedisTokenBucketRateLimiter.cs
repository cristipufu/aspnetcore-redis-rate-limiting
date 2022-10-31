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

        private static readonly LuaScript _redisScript = LuaScript.Prepare(
          @"local rate = tonumber(@tokens_per_period)
            local limit = tonumber(@token_limit)
            local requested = tonumber(@permit_count)
            local now = tonumber(@current_time)
            local period = tonumber(@replenish_period)

            local current_tokens = tonumber(redis.call(""get"", @rate_limit_key))
            if current_tokens == nil then
              current_tokens = limit
            end

            local timestamp_key = @rate_limit_key .. "":ts""
            local last_refreshed = tonumber(redis.call(""get"", timestamp_key))
            if last_refreshed == nil then
              last_refreshed = 0
            end

            local delta_ts = math.max(0, now - last_refreshed)
            local segments = math.floor(delta_ts / period)
            current_tokens = math.min(limit, current_tokens + (segments * rate))

            local allowed = current_tokens >= requested
            if allowed then
               current_tokens = current_tokens - requested
            end

            local fill_time = limit / rate
            local ttl = math.floor(fill_time * 2)

            redis.call(""setex"", @rate_limit_key, ttl, current_tokens)
            redis.call(""setex"", timestamp_key, ttl, now)

            return { allowed, current_tokens }");

        private static readonly TokenBucketLease FailedLease = new(false, null);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisTokenBucketRateLimiter(string policyName, RedisTokenBucketRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.TokensPerPeriod <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.TokensPerPeriod)), nameof(options));
            }
            if (options.ReplenishmentPeriod <= TimeSpan.Zero)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than TimeSpan.Zero.", nameof(options.ReplenishmentPeriod)), nameof(options));
            }
            if (options.TokenLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.TokenLimit)), nameof(options));
            }
            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }

            _policyName = policyName;

            _options = new RedisTokenBucketRateLimiterOptions
            {
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
                TokenLimit = options.TokenLimit,
                ReplenishmentPeriod = options.ReplenishmentPeriod,
                TokensPerPeriod = options.TokensPerPeriod,
            };

            _connectionMultiplexer = _options.ConnectionMultiplexerFactory();
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.TokenLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.TokenLimit));
            }

            var database = _connectionMultiplexer.GetDatabase();

            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = $"rl:{_policyName}",
                    current_time = nowUnixTimeSeconds,
                    tokens_per_period = _options.TokensPerPeriod,
                    token_limit = _options.TokenLimit,
                    replenish_period = _options.ReplenishmentPeriod.TotalSeconds,
                    permit_count = 1D,
                });

            bool allowed = false;
            long count = 1;

            if (response != null)
            {
                allowed = (bool)response[0];
                count = (long)response[1];
            }

            if (allowed)
            {
                return new TokenBucketLease(isAcquired: true, retryAfter: null);
            }

            return FailedLease;
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            return FailedLease;
        }

        private sealed class TokenBucketLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { MetadataName.RetryAfter.Name };

            private readonly TimeSpan? _retryAfter;

            public TokenBucketLease(bool isAcquired, TimeSpan? retryAfter)
            {
                IsAcquired = isAcquired;
                _retryAfter = retryAfter;
            }

            public override bool IsAcquired { get; }

            public override IEnumerable<string> MetadataNames => s_allMetadataNames;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (metadataName == MetadataName.RetryAfter.Name && _retryAfter.HasValue)
                {
                    metadata = _retryAfter.Value;
                    return true;
                }

                metadata = default;
                return false;
            }
        }
    }
}