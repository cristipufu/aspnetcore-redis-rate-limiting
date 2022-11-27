using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisConcurrencyManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisConcurrencyRateLimiterOptions _options;
        private readonly string RateLimitKey;
        private readonly string QueueRateLimitKey;

        private static readonly LuaScript Script = LuaScript.Prepare(
          @"local limit = tonumber(@permit_limit)
            local queue_limit = tonumber(@queue_limit)
            local try_enqueue = tonumber(@try_enqueue)
            local timestamp = tonumber(@current_time)
            -- max seconds it takes to complete a request
            local ttl = 60

            redis.call(""zremrangebyscore"", @rate_limit_key, '-inf', timestamp - ttl)

            if queue_limit > 0
            then
                redis.call(""zremrangebyscore"", @queue_key, '-inf', timestamp - ttl)
            end

            local count = redis.call(""zcard"", @rate_limit_key)
            local allowed = count < limit
            local queued = false
            local queue_count = 0

            if allowed
            then

                redis.call(""zadd"", @rate_limit_key, timestamp, @unique_id)

                if queue_limit > 0
                then
                    -- remove from pending queue
                    redis.call(""zrem"", @queue_key, @unique_id)
                end

            else

                if queue_limit > 0 and try_enqueue == 1
                then

                    queue_count = redis.call(""zcard"", @queue_key)
                    queued = queue_count < queue_limit

                    if queued
                    then
                        redis.call(""zadd"", @queue_key, timestamp, @unique_id)
                    end

                end
            end

            return { allowed, count, queued, queue_count }");

        public RedisConcurrencyManager(
            string partitionKey,
            RedisConcurrencyRateLimiterOptions options)
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
            QueueRateLimitKey = $"rl:{partitionKey}:q";
        }

        internal async Task<RedisConcurrencyResponse> TryAcquireLeaseAsync(string requestId, bool tryEnqueue = false)
        {
            var nowUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                Script,
                new
                {
                    permit_limit = _options.PermitLimit,
                    try_enqueue = tryEnqueue ? 1 : 0,
                    queue_limit = _options.QueueLimit,
                    rate_limit_key = RateLimitKey,
                    queue_key = QueueRateLimitKey,
                    current_time = nowUnixTimeSeconds,
                    unique_id = requestId,
                });

            var result = new RedisConcurrencyResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
                result.Queued = (bool)response[2];
                result.QueueCount = (long)response[3];
            }

            return result;
        }

        internal RedisConcurrencyResponse TryAcquireLease(string requestId, bool tryEnqueue = false)
        {
            var nowUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                Script,
                new
                {
                    permit_limit = _options.PermitLimit,
                    try_enqueue = tryEnqueue ? 1 : 0,
                    queue_limit = _options.QueueLimit,
                    rate_limit_key = RateLimitKey,
                    queue_key = QueueRateLimitKey,
                    current_time = nowUnixTimeSeconds,
                    unique_id = requestId,
                });

            var result = new RedisConcurrencyResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
                result.Queued = (bool)response[2];
                result.QueueCount = (long)response[3];
            }

            return result;
        }

        internal void ReleaseLease(string requestId)
        {
            var database = _connectionMultiplexer.GetDatabase();
            database.SortedSetRemove(RateLimitKey, requestId);
        }

        internal async Task ReleaseLeaseAsync(string requestId)
        {
            var database = _connectionMultiplexer.GetDatabase();
            await database.SortedSetRemoveAsync(RateLimitKey, requestId);
        }

        internal async Task ReleaseQueueLeaseAsync(string requestId)
        {
            var database = _connectionMultiplexer.GetDatabase();
            await database.SortedSetRemoveAsync(QueueRateLimitKey, requestId);
        }
    }

    internal class RedisConcurrencyResponse
    {
        internal bool Allowed { get; set; }

        internal bool Queued { get; set; }

        internal long Count { get; set; }

        internal long QueueCount { get; set; }
    }
}
