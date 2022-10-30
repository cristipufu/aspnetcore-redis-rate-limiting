using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisFixedWindowRateLimiter : RateLimiter
    {
        private readonly RedisFixedWindowRateLimiterOptions _options;
        private readonly string _policyName;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        private static readonly LuaScript _incrementScript = LuaScript.Prepare(
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

        private static readonly RateLimitLease SuccessfulLease = new FixedWindowLease(true, null);
        private static readonly RateLimitLease FailedLease = new FixedWindowLease(false, null);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisFixedWindowRateLimiter(string policyName, RedisFixedWindowRateLimiterOptions options)
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
            if (options.ConnectionMultiplexer is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexer)), nameof(options));
            }

            _options = new RedisFixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = options.Window,
                ConnectionMultiplexer = options.ConnectionMultiplexer,
            };

            _policyName = policyName;

            _connectionMultiplexer = _options.ConnectionMultiplexer;
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            throw new NotImplementedException();
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            return new ValueTask<RateLimitLease>(FailedLease);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var database = _connectionMultiplexer.GetDatabase();

            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                _incrementScript,
                new
                {
                    rate_limit_key = $"rl:{_policyName}",
                    next_expires_at = now.Add(_options.Window).ToUnixTimeSeconds(),
                    current_time = nowUnixTimeSeconds,
                    increment_amount = 1D,
                });

            long count = 1;
            long expireAt = nowUnixTimeSeconds;

            if (response != null)
            {
                count = (long)response[0];
                expireAt = (long)response[1];
            }

            if (count > _options.PermitLimit)
            {
                return new FixedWindowLease(isAcquired: false, TimeSpan.FromSeconds(expireAt - nowUnixTimeSeconds));
            }

            return SuccessfulLease;
        }

        private sealed class FixedWindowLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { MetadataName.RetryAfter.Name };

            private readonly TimeSpan? _retryAfter;

            public FixedWindowLease(bool isAcquired, TimeSpan? retryAfter)
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
