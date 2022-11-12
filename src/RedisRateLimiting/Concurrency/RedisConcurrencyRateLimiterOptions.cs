namespace RedisRateLimiting
{
    /// <summary>
    /// Options to specify the behavior of a <see cref="RedisConcurrencyRateLimiterOptions"/>.
    /// </summary>
    public sealed class RedisConcurrencyRateLimiterOptions : RedisRateLimiterOptions
    {
        /// <summary>
        /// Maximum number of permits that can be leased concurrently.
        /// Must be set to a value > 0 by the time these options are passed to the constructor of <see cref="RedisConcurrencyRateLimiter{TKey}"/>.
        /// </summary>
        public int PermitLimit { get; set; }

        /// <summary>
        /// Maximum number of permits that can be queued concurrently.
        /// Must be set to a value >= 0 by the time these options are passed to the constructor of <see cref="RedisConcurrencyRateLimiter{TKey}"/>.
        /// </summary>
        public int QueueLimit { get; set; }
    }
}
