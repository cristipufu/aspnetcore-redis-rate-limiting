using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Threading.RateLimiting;

namespace RedisRateLimiting
{
    public static class RedisRateLimiterOptionsExtensions
    {
        /// <summary>
        /// Adds a new <see cref="RedisConcurrencyRateLimiter"/> with the given <see cref="RedisConcurrencyRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisConcurrencyRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisConcurrencyLimiter(this RateLimiterOptions options, string policyName, Action<RedisConcurrencyRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var concurrencyLimiterOptions = new RedisConcurrencyRateLimiterOptions();
            configureOptions.Invoke(concurrencyLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return new RateLimitPartition<string>(policyName, policyName => new RedisConcurrencyRateLimiter(policyName, concurrencyLimiterOptions!));
            });
        }

        /// <summary>
        /// Adds a new <see cref="RedisFixedWindowRateLimiter"/> with the given <see cref="RedisFixedWindowRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisFixedWindowRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisFixedWindowLimiter(this RateLimiterOptions options, string policyName, Action<RedisFixedWindowRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var fixedWindowRateLimiterOptions = new RedisFixedWindowRateLimiterOptions();
            configureOptions.Invoke(fixedWindowRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return new RateLimitPartition<string>(policyName, policyName => new RedisFixedWindowRateLimiter(policyName, fixedWindowRateLimiterOptions!));
            });
        }

        /// <summary>
        /// Adds a new <see cref="RedisTokenBucketRateLimiter"/> with the given <see cref="RedisTokenBucketRateLimiterOptions"/> to the <see cref="RateLimiterOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="RateLimiterOptions"/> to add a limiter to.</param>
        /// <param name="policyName">The name that will be associated with the limiter.</param>
        /// <param name="configureOptions">A callback to configure the <see cref="RedisTokenBucketRateLimiterOptions"/> to be used for the limiter.</param>
        /// <returns>This <see cref="RateLimiterOptions"/>.</returns>
        public static RateLimiterOptions AddRedisTokenBucketLimiter(this RateLimiterOptions options, string policyName, Action<RedisTokenBucketRateLimiterOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            var tokenBucketRateLimiterOptions = new RedisTokenBucketRateLimiterOptions();
            configureOptions.Invoke(tokenBucketRateLimiterOptions);

            return options.AddPolicy(policyName, context =>
            {
                return new RateLimitPartition<string>(policyName, policyName => new RedisTokenBucketRateLimiter(policyName, tokenBucketRateLimiterOptions!));
            });
        }
    }
}