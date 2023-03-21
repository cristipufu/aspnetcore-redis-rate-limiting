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
        private readonly RedisKey RateLimitTimestampKey;

        private static readonly LuaScript _redisScript = LuaScript.Prepare(@"
            -- Prepare the input and force the correct data types.
            local limit = tonumber(@token_limit)
            local rate = tonumber(@tokens_per_period)
            local period = tonumber(@replenish_period)
            local requested = tonumber(@permit_count)

            -- Even though it is the default since Redis 5, we explicitly enable command replication.
            -- This ensures that non-deterministic commands like 'TIME' are replicated by effect.
            redis.replicate_commands()

            -- Retrieve the current time as unix timestamp with millisecond accuracy.
            local time = redis.call('TIME')
            local now = math.floor((time[1] * 1000) + (time[2] / 1000))

            -- Load the current state from Redis. We use MGET to save a round-trip.
            local state = redis.call('MGET', @rate_limit_key, @timestamp_key)
            local current_tokens = tonumber(state[1]) or limit
            local last_refreshed = tonumber(state[2]) or 0

            -- Calculate the time and replenishment periods elapsed since the last call.
            local time_since_last_refreshed = math.max(0, now - last_refreshed)
            local periods_since_last_refreshed = math.floor(time_since_last_refreshed / period)

            -- Now we have all the info we need to calculate the current tokens based on the elapsed time.
            current_tokens = math.min(limit, current_tokens + (periods_since_last_refreshed * rate))

            -- If the bucket contains enough tokens for the current request, we remove the tokens.
            local allowed = current_tokens >= requested
            if allowed then
               current_tokens = current_tokens - requested
            end

            -- In order to remove rate limit keys automatically from the database, we calculate a TTL
            -- based on the worst-case scenario for the bucket to fill up again.
            local periods_until_full = limit / rate
            local ttl = math.ceil(periods_until_full * period)

            -- We only store the new state in the database if the request was granted.
            -- This avoids rounding issues and edge cases which can occur if many requests are rate limited.
            if allowed then
                local time_of_last_replenishment = now
                if last_refreshed > 0 then
                    time_of_last_replenishment = last_refreshed + (periods_since_last_refreshed * period)
                end

                redis.call('SET', @rate_limit_key, current_tokens, 'EX', ttl)
                redis.call('SET', @timestamp_key, time_of_last_replenishment, 'EX', ttl)
            end

            return { allowed, current_tokens }");

        public RedisTokenBucketManager(
            string partitionKey,
            RedisTokenBucketRateLimiterOptions options)
        {
            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory!.Invoke();

            RateLimitKey = new RedisKey($"rl:{{{partitionKey}}}");
            RateLimitTimestampKey = new RedisKey($"rl:{{{partitionKey}}}:ts");
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
                    rate_limit_key = (RedisKey)RateLimitKey,
                    timestamp_key = (RedisKey)RateLimitTimestampKey,
                    tokens_per_period = (RedisValue)_options.TokensPerPeriod,
                    token_limit = (RedisValue)_options.TokenLimit,
                    replenish_period = (RedisValue)_options.ReplenishmentPeriod.TotalMilliseconds,
                    permit_count = (RedisValue)1D,
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
            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                _redisScript,
                new
                {
                    rate_limit_key = (RedisKey)RateLimitKey,
                    timestamp_key = (RedisKey)RateLimitTimestampKey,
                    tokens_per_period = (RedisValue)_options.TokensPerPeriod,
                    token_limit = (RedisValue)_options.TokenLimit,
                    replenish_period = (RedisValue)_options.ReplenishmentPeriod.TotalMilliseconds,
                    permit_count = (RedisValue)1D,
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
