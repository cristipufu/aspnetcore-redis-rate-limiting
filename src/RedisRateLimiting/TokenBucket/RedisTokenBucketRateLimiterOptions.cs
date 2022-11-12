using System;

namespace RedisRateLimiting
{
    /// <summary>
    /// Options to specify the behavior of a <see cref="RedisTokenBucketRateLimiterOptions"/>.
    /// </summary>
    public sealed class RedisTokenBucketRateLimiterOptions : RedisRateLimiterOptions
    {
        /// <summary>
        /// Specifies the minimum period between replenishments.
        /// Must be set to a value greater than <see cref="TimeSpan.Zero" /> by the time these options are passed to the constructor of <see cref="RedisTokenBucketRateLimiter"/>.
        /// </summary>
        public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Specifies the maximum number of tokens to restore each replenishment.
        /// Must be set to a value > 0 by the time these options are passed to the constructor of <see cref="RedisTokenBucketRateLimiter{TKey}"/>.
        /// </summary>
        public int TokensPerPeriod { get; set; }

        /// <summary>
        /// Maximum number of tokens that can be in the bucket at any time.
        /// Must be set to a value > 0 by the time these options are passed to the constructor of <see cref="RedisTokenBucketRateLimiter{TKey}"/>.
        /// </summary>
        public int TokenLimit { get; set; }
    }
}
