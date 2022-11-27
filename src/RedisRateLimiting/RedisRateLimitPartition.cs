using System;
using System.Threading.RateLimiting;

namespace RedisRateLimiting
{
    /// <summary>
    /// Contains methods for assisting in the creation of partitions for your rate limiter.
    /// </summary>
    public static class RedisRateLimitPartition
    {
        /// <summary>
        /// Defines a partition with a <see cref="RedisConcurrencyRateLimiter{TKey}"/> with the given <see cref="RedisConcurrencyRateLimiterOptions"/>.
        /// </summary>
        /// <typeparam name="TKey">The type to distinguish partitions with.</typeparam>
        /// <param name="partitionKey">The specific key for this partition. This will be used to check for an existing cached limiter before calling the <paramref name="factory"/>.</param>
        /// <param name="factory">The function called when a rate limiter for the given <paramref name="partitionKey"/> is needed. This can return the same instance of <see cref="RedisConcurrencyRateLimiterOptions"/> across different calls.</param>
        /// <returns></returns>
        public static RateLimitPartition<TKey> GetConcurrencyRateLimiter<TKey>(
            TKey partitionKey,
            Func<TKey, RedisConcurrencyRateLimiterOptions> factory)
        {
            return RateLimitPartition.Get(partitionKey, key => new RedisConcurrencyRateLimiter<TKey>(key, factory(key)));
        }

        /// <summary>
        /// Defines a partition with a <see cref="RedisFixedWindowRateLimiter{TKey}"/> with the given <see cref="RedisFixedWindowRateLimiterOptions"/>.
        /// </summary>
        /// <typeparam name="TKey">The type to distinguish partitions with.</typeparam>
        /// <param name="partitionKey">The specific key for this partition. This will be used to check for an existing cached limiter before calling the <paramref name="factory"/>.</param>
        /// <param name="factory">The function called when a rate limiter for the given <paramref name="partitionKey"/> is needed. This can return the same instance of <see cref="RedisFixedWindowRateLimiterOptions"/> across different calls.</param>
        /// <returns></returns>
        public static RateLimitPartition<TKey> GetFixedWindowRateLimiter<TKey>(
            TKey partitionKey,
            Func<TKey, RedisFixedWindowRateLimiterOptions> factory)
        {
            return RateLimitPartition.Get(partitionKey, key => new RedisFixedWindowRateLimiter<TKey>(key, factory(key)));
        }

        /// <summary>
        /// Defines a partition with a <see cref="RedisSlidingWindowRateLimiter{TKey}"/> with the given <see cref="RedisSlidingWindowRateLimiterOptions"/>.
        /// </summary>
        /// <typeparam name="TKey">The type to distinguish partitions with.</typeparam>
        /// <param name="partitionKey">The specific key for this partition. This will be used to check for an existing cached limiter before calling the <paramref name="factory"/>.</param>
        /// <param name="factory">The function called when a rate limiter for the given <paramref name="partitionKey"/> is needed. This can return the same instance of <see cref="RedisSlidingWindowRateLimiterOptions"/> across different calls.</param>
        /// <returns></returns>
        public static RateLimitPartition<TKey> GetSlidingWindowRateLimiter<TKey>(
            TKey partitionKey,
            Func<TKey, RedisSlidingWindowRateLimiterOptions> factory)
        {
            return RateLimitPartition.Get(partitionKey, key => new RedisSlidingWindowRateLimiter<TKey>(key, factory(key)));
        }

        /// <summary>
        /// Defines a partition with a <see cref="RedisTokenBucketRateLimiter{TKey}"/> with the given <see cref="RedisTokenBucketRateLimiterOptions"/>.
        /// </summary>
        /// <typeparam name="TKey">The type to distinguish partitions with.</typeparam>
        /// <param name="partitionKey">The specific key for this partition. This will be used to check for an existing cached limiter before calling the <paramref name="factory"/>.</param>
        /// <param name="factory">The function called when a rate limiter for the given <paramref name="partitionKey"/> is needed. This can return the same instance of <see cref="RedisTokenBucketRateLimiterOptions"/> across different calls.</param>
        /// <returns></returns>
        public static RateLimitPartition<TKey> GetTokenBucketRateLimiter<TKey>(
            TKey partitionKey,
            Func<TKey, RedisTokenBucketRateLimiterOptions> factory)
        {
            return RateLimitPartition.Get(partitionKey, key => new RedisTokenBucketRateLimiter<TKey>(key, factory(key)));
        }
    }
}
