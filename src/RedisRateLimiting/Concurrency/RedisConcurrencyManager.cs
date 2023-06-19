using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal class RedisConcurrencyManager
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly RedisConcurrencyRateLimiterOptions _options;
        private readonly RedisKey RateLimitKey;
        private readonly RedisKey QueueRateLimitKey;
        private readonly RedisKey StatsRateLimitKey;

        private static readonly LuaScript Script = LuaScript.Prepare(
          @"local limit = tonumber(@permit_limit)
            local queue_limit = tonumber(@queue_limit)
            local try_enqueue = tonumber(@try_enqueue)
            local timestamp = tonumber(@current_time)
            local requested = tonumber(@permit_count)
            -- max seconds it takes to complete a request
            local ttl = 60

            redis.call(""zremrangebyscore"", @rate_limit_key, '-inf', timestamp - ttl)

            if queue_limit > 0
            then
                redis.call(""zremrangebyscore"", @queue_key, '-inf', timestamp - ttl)
            end

            local count = redis.call(""zcard"", @rate_limit_key)
            local allowed = count + requested <= limit
            local queued = false
            local queue_count = 0

            local addparams = {}
            local remparams = {}
            for i=1,requested do
                local index = i*2
                addparams[index-1]=timestamp
                addparams[index]=@unique_id..':'..tostring(i)
                remparams[i]=addparams[index]
            end

            if allowed
            then

                if queue_limit > 0
                then
                    queue_count = redis.call(""zcard"", @queue_key)
                end

                
                if queue_count == 0 or try_enqueue == 0
                then

                    redis.call(""zadd"", @rate_limit_key, unpack(addparams))

                    if queue_limit > 0
                    then
                        -- remove from pending queue
                        redis.call(""zrem"", @queue_key, unpack(remparams))
                    end
                
                else
                    -- queue the current request next in line if we have any requests in the pending queue
                    allowed = false

                    queued = queue_count + count + requested <= limit + queue_limit

                    if queued
                    then
                        redis.call(""zadd"", @queue_key, unpack(addparams))
                    end

                end
            
            else
                -- try to queue request
                if queue_limit > 0 and try_enqueue == 1
                then

                    queue_count = redis.call(""zcard"", @queue_key)
                    queued = queue_count + requested <= queue_limit

                    if queued
                    then
                        redis.call(""zadd"", @queue_key, unpack(addparams))
                    end

                end
            end

            if allowed
            then
                redis.call(""hincrby"", @stats_key, 'total_successful', requested)
            else
                if queued == false and try_enqueue == 1
                then
                    redis.call(""hincrby"", @stats_key, 'total_failed', requested)
                end
            end

            return { allowed, count, queued, queue_count }");

        private static readonly LuaScript StatisticsScript = LuaScript.Prepare(
          @"local count = redis.call(""zcard"", @rate_limit_key)
            local queue_count = redis.call(""zcard"", @queue_key)
            local total_successful_count = redis.call(""hget"", @stats_key, 'total_successful')
            local total_failed_count = redis.call(""hget"", @stats_key, 'total_failed')

            return { count, queue_count, total_successful_count, total_failed_count }");

        public RedisConcurrencyManager(
            string partitionKey,
            RedisConcurrencyRateLimiterOptions options)
        {
            _options = options;
            _connectionMultiplexer = options.ConnectionMultiplexerFactory!.Invoke();

            RateLimitKey = new RedisKey($"rl:{{{partitionKey}}}");
            QueueRateLimitKey = new RedisKey($"rl:{{{partitionKey}}}:q");
            StatsRateLimitKey = new RedisKey($"rl:{{{partitionKey}}}:stats");
        }

        internal async Task<RedisConcurrencyResponse> TryAcquireLeaseAsync(string requestId, int permitCount, bool tryEnqueue = false)
        {
            var nowUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                Script,
                new
                {
                    rate_limit_key = RateLimitKey,
                    queue_key = QueueRateLimitKey,
                    stats_key = StatsRateLimitKey,
                    permit_limit = (RedisValue)_options.PermitLimit,
                    try_enqueue = (RedisValue)(tryEnqueue ? 1 : 0),
                    permit_count = (RedisValue)permitCount,
                    queue_limit = (RedisValue)_options.QueueLimit,
                    current_time = (RedisValue)nowUnixTimeSeconds,
                    unique_id = (RedisValue)requestId,
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

        internal RedisConcurrencyResponse TryAcquireLease(string requestId, int permitCount, bool tryEnqueue = false)
        {
            var nowUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                Script,
                new
                {
                    rate_limit_key = RateLimitKey,
                    queue_key = QueueRateLimitKey,
                    stats_key = StatsRateLimitKey,
                    permit_limit = (RedisValue)_options.PermitLimit,
                    try_enqueue = (RedisValue)(tryEnqueue ? 1 : 0),
                    permit_count = (RedisValue)permitCount,
                    queue_limit = (RedisValue)_options.QueueLimit,
                    current_time = (RedisValue)nowUnixTimeSeconds,
                    unique_id = (RedisValue)requestId,
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

        internal void ReleaseLease(string requestId, int permitCount)
        {
            var database = _connectionMultiplexer.GetDatabase();

            for (var i = 1; i <= permitCount; i++)
            {
                database.SortedSetRemove(RateLimitKey, $"{requestId}:{i}");
            }
        }

        internal Task ReleaseLeaseAsync(string requestId, int permitCount)
        {
            var database = _connectionMultiplexer.GetDatabase();
            var tasks = new List<Task>(permitCount);

            for (var i = 1; i <= permitCount; i++)
            {
                tasks.Add(database.SortedSetRemoveAsync(RateLimitKey, $"{requestId}:{i}"));
            }

            return Task.WhenAll(tasks);
        }

        internal Task ReleaseQueueLeaseAsync(string requestId, int permitCount)
        {
            var database = _connectionMultiplexer.GetDatabase();

            var tasks = new List<Task>(permitCount);

            for (var i = 1; i <= permitCount; i++)
            {
                tasks.Add(database.SortedSetRemoveAsync(QueueRateLimitKey, $"{requestId}:{i}"));
            }

            return Task.WhenAll(tasks);
        }

        internal RateLimiterStatistics? GetStatistics()
        {
            var database = _connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                StatisticsScript,
                new
                {
                    rate_limit_key = RateLimitKey,
                    queue_key = QueueRateLimitKey,
                    stats_key = StatsRateLimitKey,
                });

            if (response == null)
            {
                return null;
            }

            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = _options.PermitLimit + _options.QueueLimit - (long)response[0] - (long)response[1],
                CurrentQueuedCount = (long)response[1],
                TotalSuccessfulLeases = (long)response[2],
                TotalFailedLeases = (long)response[3],
            };
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
