using Microsoft.AspNetCore.RateLimiting;

namespace RedisRateLimiting.AspNetCore
{
    public static class RedisRateLimiterOptionsExtensions
    {
        /// <summary>
        /// Adds a new <see cref="RedisConcurrencyRateLimiter{TKey}"/> with the given <see cref="RedisConcurrencyRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisConcurrencyRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisConcurrencyLimiter(this RateLimiterOptions options, string policyName, Action<RedisConcurrencyRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var key = new PolicyNameKey() { PolicyName = policyName };
            var concurrencyRateLimiterOptions = new RedisConcurrencyRateLimiterOptions();
            configureOptions.Invoke(concurrencyRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return RedisRateLimitPartition.GetRedisConcurrencyRateLimiter(key, _ => concurrencyRateLimiterOptions);
            });
        }

        /// <summary>
        /// Adds a new <see cref="RedisFixedWindowRateLimiter{TKey}"/> with the given <see cref="RedisFixedWindowRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisFixedWindowRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisFixedWindowLimiter(this RateLimiterOptions options, string policyName, Action<RedisFixedWindowRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var key = new PolicyNameKey() { PolicyName = policyName };
            var fixedWindowRateLimiterOptions = new RedisFixedWindowRateLimiterOptions();
            configureOptions.Invoke(fixedWindowRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return RedisRateLimitPartition.GetRedisFixedWindowRateLimiter(key, _ => fixedWindowRateLimiterOptions);
            });
        }

        /// <summary>
        /// Adds a new <see cref="RedisSlidingWindowRateLimiter{TKey}"/> with the given <see cref="RedisSlidingWindowRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisSlidingWindowRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisSlidingWindowLimiter(this RateLimiterOptions options, string policyName, Action<RedisSlidingWindowRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var key = new PolicyNameKey() { PolicyName = policyName };
            var slidingWindowRateLimiterOptions = new RedisSlidingWindowRateLimiterOptions();
            configureOptions.Invoke(slidingWindowRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return RedisRateLimitPartition.GetRedisSlidingWindowRateLimiter(key, _ => slidingWindowRateLimiterOptions);
            });
        }

        /// <summary>
        /// Adds a new <see cref="RedisTokenBucketRateLimiter{TKey}"/> with the given <see cref="RedisTokenBucketRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisTokenBucketRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisTokenBucketLimiter(this RateLimiterOptions options, string policyName, Action<RedisTokenBucketRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var key = new PolicyNameKey() { PolicyName = policyName };
            var tokenBucketRateLimiterOptions = new RedisTokenBucketRateLimiterOptions();
            configureOptions.Invoke(tokenBucketRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return RedisRateLimitPartition.GetRedisTokenBucketRateLimiter(key, _ => tokenBucketRateLimiterOptions);
            });
        }
    }
}