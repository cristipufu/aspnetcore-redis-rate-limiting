﻿using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisFixedWindowManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisFixedWindowRateLimiterOptions _options;
        private readonly RedisKey RateLimitKey;
        private readonly RedisKey RateLimitExpireKey;

        private static readonly LuaScript _redisScript = LuaScript.Prepare(
          @"local expires_at = tonumber(redis.call(""get"", @expires_at_key))
            local current = tonumber(redis.call(""get"", @rate_limit_key))
            local requested = tonumber(@increment_amount)
            local limit = tonumber(@permit_limit)

            if not expires_at or expires_at < tonumber(@current_time) then
                -- this is either a brand new window,
                -- or this window has closed, but redis hasn't cleaned up the key yet
                -- (redis will clean it up in one more second)
                -- initialize a new rate limit window
                redis.call(""set"", @rate_limit_key, 0)
                redis.call(""set"", @expires_at_key, @next_expires_at)
                -- tell Redis to clean this up _one second after_ the expires_at time (clock differences).
                -- (Redis will only clean up these keys long after the window has passed)
                redis.call(""expireat"", @rate_limit_key, @next_expires_at + 1)
                redis.call(""expireat"", @expires_at_key, @next_expires_at + 1)
                -- since the database was updated, return the new value
                expires_at = @next_expires_at
                current = 0
            end

            local allowed = current + requested <= limit

            if allowed
            then
                -- now that the window either already exists or it was freshly initialized,
                -- increment the counter(`incrby` returns a number)
                current = redis.call(""incrby"", @rate_limit_key, @increment_amount)
            end

            return { current, expires_at, allowed }");

        public RedisFixedWindowManager(
            string partitionKey,
            RedisFixedWindowRateLimiterOptions options)
        {
            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory!.Invoke();

            RateLimitKey = new RedisKey($"rl:{{{partitionKey}}}");
            RateLimitExpireKey = new RedisKey($"rl:{{{partitionKey}}}:exp");
        }

        internal async Task<RedisFixedWindowResponse> TryAcquireLeaseAsync(int permitCount)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    expires_at_key = RateLimitExpireKey,
                    permit_limit = _options.PermitLimit,
                    next_expires_at = now.Add(_options.Window).ToUnixTimeSeconds(),
                    current_time = nowUnixTimeSeconds,
                    increment_amount = permitCount,
                });

            var result = new RedisFixedWindowResponse();

            if (response != null)
            {
                result.Count = (long)response[0];
                result.ExpiresAt = (long)response[1];
                result.Allowed = (bool)response[2];
                result.RetryAfter = TimeSpan.FromSeconds(result.ExpiresAt - nowUnixTimeSeconds);
            }

            return result;
        }

        internal RedisFixedWindowResponse TryAcquireLease(int permitCount)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                _redisScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    expires_at_key = RateLimitExpireKey,
                    permit_limit = _options.PermitLimit,
                    next_expires_at = now.Add(_options.Window).ToUnixTimeSeconds(),
                    current_time = nowUnixTimeSeconds,
                    increment_amount = permitCount,
                });

            var result = new RedisFixedWindowResponse();

            if (response != null)
            {
                result.Count = (long)response[0];
                result.ExpiresAt = (long)response[1];
                result.Allowed = (bool)response[2];
                result.RetryAfter = TimeSpan.FromSeconds(result.ExpiresAt - nowUnixTimeSeconds);
            }

            return result;
        }
    }

    internal class RedisFixedWindowResponse
    {
        internal long ExpiresAt { get; set; }
        internal TimeSpan RetryAfter { get; set; }
        internal long Count { get; set; }
        internal bool Allowed { get; set; }
    }
}
