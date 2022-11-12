using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace RedisRateLimiting.Concurrency
{
    internal static class RedisConcurrencyScript
    {
        internal static readonly LuaScript Script = LuaScript.Prepare(
          @"local limit = tonumber(@permit_limit)
            local queue_limit = tonumber(@queue_limit)
            local timestamp = tonumber(@current_time)
            local ttl = 60

            redis.call(""zremrangebyscore"", @rate_limit_key, '-inf', timestamp - ttl)
            redis.call(""zremrangebyscore"", @queue_key, '-inf', timestamp - ttl)

            local count = redis.call(""zcard"", @rate_limit_key)
            local allowed = count < limit
            local queued = false

            if allowed
            then
                redis.call(""zadd"", @rate_limit_key, timestamp, @unique_id)
                -- remove from pending queue
                redis.call(""zrem"", @queue_key, @unique_id)
            else
                local queue_count = redis.call(""zcard"", @queue_key)
                queued = queue_count < queue_limit
                if queued
                then
                    redis.call(""zadd"", @queue_key, timestamp, @unique_id)
                end
            end

            return { allowed, count, queued }");

        internal static async Task<RedisConcurrencyScriptResponse> ExecuteConcurrencyScriptAsync(
            this IConnectionMultiplexer connectionMultiplexer, RedisConcurrencyScriptRequest request)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)await database.ScriptEvaluateAsync(
                Script,
                new
                {
                    permit_limit = request.PermitLimit,
                    queue_limit = request.QueueLimit,
                    rate_limit_key = request.PartitionKey,
                    queue_key = request.QueueKey,
                    current_time = nowUnixTimeSeconds,
                    unique_id = request.RequestId,
                });

            var result = new RedisConcurrencyScriptResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
                result.Queued = (bool)response[2];
            }

            return result;
        }

        internal static RedisConcurrencyScriptResponse ExecuteConcurrencyScript(
            this IConnectionMultiplexer connectionMultiplexer, RedisConcurrencyScriptRequest request)
        {
            var now = DateTimeOffset.UtcNow;
            var nowUnixTimeSeconds = now.ToUnixTimeSeconds();

            var database = connectionMultiplexer.GetDatabase();

            var response = (RedisValue[]?)database.ScriptEvaluate(
                Script,
                new
                {
                    permit_limit = request.PermitLimit,
                    queue_limit = request.QueueLimit,
                    rate_limit_key = request.PartitionKey,
                    queue_key = request.QueueKey,
                    current_time = nowUnixTimeSeconds,
                    unique_id = request.RequestId,
                });

            var result = new RedisConcurrencyScriptResponse();

            if (response != null)
            {
                result.Allowed = (bool)response[0];
                result.Count = (long)response[1];
                result.Queued = (bool)response[2];
            }

            return result;
        }
    }

    internal class RedisConcurrencyScriptResponse
    {
        internal bool Allowed { get; set; }

        internal bool Queued { get; set; }

        internal long Count { get; set; }
    }

    internal class RedisConcurrencyScriptRequest
    {
        internal int PermitLimit { get; set; }

        internal int QueueLimit { get; set; }

        internal string? PartitionKey { get; set; }
        
        internal string? QueueKey { get; set; }

        internal string? RequestId { get; set; }
    }
}
