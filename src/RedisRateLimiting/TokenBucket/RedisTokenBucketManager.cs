using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisTokenBucketManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisTokenBucketRateLimiterOptions _options;
        private readonly RedisKey RateLimitKey;

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

        public RedisTokenBucketManager(
            string partitionKey,
            RedisTokenBucketRateLimiterOptions options)
        {
            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory!.Invoke();

            RateLimitKey = new RedisKey($"rl:{partitionKey}");
        }

        internal async Task<RedisTokenBucketResponse> TryAcquireLeaseAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    current_time = nowUnixTimeSeconds,
                    tokens_per_period = _options.TokensPerPeriod,
                    token_limit = _options.TokenLimit,
                    replenish_period = _options.ReplenishmentPeriod.TotalSeconds,
                    permit_count = 1D,
                });

            var result = new RedisTokenBucketResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
            }

            return result;
        }

        internal RedisTokenBucketResponse TryAcquireLease()
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    current_time = nowUnixTimeSeconds,
                    tokens_per_period = _options.TokensPerPeriod,
                    token_limit = _options.TokenLimit,
                    replenish_period = _options.ReplenishmentPeriod.TotalSeconds,
                    permit_count = 1D,
                });

            var result = new RedisTokenBucketResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
            }

            return result;
        }
    }

    internal class RedisTokenBucketResponse
    {
        internal bool Allowed { get; set; }
        internal long Count { get; set; }
    }
}
