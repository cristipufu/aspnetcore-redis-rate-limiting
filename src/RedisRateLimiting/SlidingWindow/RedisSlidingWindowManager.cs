using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisSlidingWindowManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisSlidingWindowRateLimiterOptions _options;
        private readonly string RateLimitKey;

        private static readonly LuaScript _redisScript = LuaScript.Prepare(
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

            return { allowed, count }");

        public RedisSlidingWindowManager(
            string partitionKey,
            RedisSlidingWindowRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }

            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory.Invoke();

            RateLimitKey = $"rl:{partitionKey}";
        }

        internal async Task<RedisSlidingWindowResponse> TryAcquireLeaseAsync(string requestId)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    permit_limit = _options.PermitLimit,
                    window = _options.Window.TotalSeconds,
                    current_time = nowUnixTimeSeconds,
                    unique_id = requestId,
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
               _redisScript,
               new
               {
                   rate_limit_key = RateLimitKey,
                   permit_limit = _options.PermitLimit,
                   window = _options.Window.TotalSeconds,
                   current_time = nowUnixTimeSeconds,
                   unique_id = requestId,
               });

            var result = new RedisSlidingWindowResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
            }

            return result;
        }
    }

    internal class RedisSlidingWindowResponse
    {
        internal bool Allowed { get; set; }
        internal long Count { get; set; }
    }
}
