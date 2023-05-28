using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisSlidingWindowManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisSlidingWindowRateLimiterOptions _options;
        private readonly RedisKey RateLimitKey;
        private readonly RedisKey StatsRateLimitKey;

        private static readonly LuaScript Script = LuaScript.Prepare(
          @"local limit = tonumber(@permit_limit)
            local timestamp = tonumber(@current_time)
            local window = tonumber(@window)

            -- remove all requests outside current window
            redis.call(""zremrangebyscore"", @rate_limit_key, '-inf', timestamp - window)

            local count = redis.call(""zcard"", @rate_limit_key)
            local allowed = count < limit

            if allowed
            then
                redis.call(""zadd"", @rate_limit_key, timestamp, @unique_id)
            end

            redis.call(""expireat"", @rate_limit_key, timestamp + window + 1)

            if allowed
            then
                redis.call(""hincrby"", @stats_key, 'total_successful', 1)
            else
                redis.call(""hincrby"", @stats_key, 'total_failed', 1)
            end

            return { allowed, count }");

        private static readonly LuaScript StatisticsScript = LuaScript.Prepare(
            @"local count = redis.call(""zcard"", @rate_limit_key)
            local total_successful_count = redis.call(""hget"", @stats_key, 'total_successful')
            local total_failed_count = redis.call(""hget"", @stats_key, 'total_failed')

            return { count, total_successful_count, total_failed_count }");

        public RedisSlidingWindowManager(
            string partitionKey,
            RedisSlidingWindowRateLimiterOptions options)
        {
            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory!.Invoke();

            RateLimitKey = new RedisKey($"rl:{{{partitionKey}}}");
            StatsRateLimitKey = new RedisKey($"rl:{{{partitionKey}}}:stats");
        }

        internal async Task<RedisSlidingWindowResponse> TryAcquireLeaseAsync(string requestId)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                Script,
                new
                {
                    rate_limit_key = RateLimitKey,
                    stats_key = StatsRateLimitKey,
                    permit_limit = (RedisValue)_options.PermitLimit,
                    window = (RedisValue)_options.Window.TotalSeconds,
                    current_time = (RedisValue)nowUnixTimeSeconds,
                    unique_id = (RedisValue)requestId,
                });

            var result = new RedisSlidingWindowResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
            }

            return result;
        }

        internal RedisSlidingWindowResponse TryAcquireLease(string requestId)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
               Script,
               new
               {
                   rate_limit_key = RateLimitKey,
                   stats_key = StatsRateLimitKey,
                   permit_limit = (RedisValue)_options.PermitLimit,
                   window = (RedisValue)_options.Window.TotalSeconds,
                   current_time = (RedisValue)nowUnixTimeSeconds,
                   unique_id = (RedisValue)requestId,
               });

            var result = new RedisSlidingWindowResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
            }

            return result;
        }

        internal RateLimiterStatistics? GetStatistics()
        {
            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                StatisticsScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    stats_key = StatsRateLimitKey,
                });

            if (response == null)
            {
                return null;
            }

            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = _options.PermitLimit - (long)response[0],
                TotalSuccessfulLeases = (long)response[1],
                TotalFailedLeases = (long)response[2],
            };
        }
    }

    internal class RedisSlidingWindowResponse
    {
        internal bool Allowed { get; set; }
        internal long Count { get; set; }
    }
}
