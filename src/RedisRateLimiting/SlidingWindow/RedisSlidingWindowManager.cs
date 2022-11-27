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
          @"local expires_at_key = @rate_limit_key .. "":exp""
            local expires_at = tonumber(redis.call(""get"", expires_at_key))

            if not expires_at or expires_at < tonumber(@current_time) then
                -- this is either a brand new window,
                -- or this window has closed, but redis hasn't cleaned up the key yet
                -- (redis will clean it up in one more second)
                -- initialize a new rate limit window
                redis.call(""set"", @rate_limit_key, 0)
                redis.call(""set"", expires_at_key, @next_expires_at)
                -- tell Redis to clean this up _one second after_ the expires_at time (clock differences).
                -- (Redis will only clean up these keys long after the window has passed)
                redis.call(""expireat"", @rate_limit_key, @next_expires_at + 1)
                redis.call(""expireat"", expires_at_key, @next_expires_at + 1)
                -- since the database was updated, return the new value
                expires_at = @next_expires_at
            end

            -- now that the window either already exists or it was freshly initialized,
            -- increment the counter(`incrby` returns a number)
            local current = redis.call(""incrby"", @rate_limit_key, @increment_amount)

            return { current, expires_at }");

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

        internal async Task<RedisSlidingWindowResponse> TryAcquireLeaseAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    next_expires_at = now.Add(_options.Window).ToUnixTimeSeconds(),
                    current_time = nowUnixTimeSeconds,
                    increment_amount = 1D,
                });

            var result = new RedisSlidingWindowResponse();

            if (response != null)
            {
                result.Count = (long)response[0];
                result.ExpiresAt = (long)response[1];
                result.RetryAfter = TimeSpan.FromSeconds(result.ExpiresAt - nowUnixTimeSeconds);
            }

            return result;
        }

        internal RedisSlidingWindowResponse TryAcquireLease()
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    next_expires_at = now.Add(_options.Window).ToUnixTimeSeconds(),
                    current_time = nowUnixTimeSeconds,
                    increment_amount = 1D,
                });

            var result = new RedisSlidingWindowResponse();

            if (response != null)
            {
                result.Count = (long)response[0];
                result.ExpiresAt = (long)response[1];
                result.RetryAfter = TimeSpan.FromSeconds(result.ExpiresAt - nowUnixTimeSeconds);
            }

            return result;
        }
    }

    internal class RedisSlidingWindowResponse
    {
        internal long ExpiresAt { get; set; }
        internal TimeSpan RetryAfter { get; set; }
        internal long Count { get; set; }
    }
}
