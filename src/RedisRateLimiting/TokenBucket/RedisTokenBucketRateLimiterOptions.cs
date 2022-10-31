namespace RedisRateLimiting
{
    /// <summary>
    /// Options to specify the behavior of a <see cref="RedisTokenBucketRateLimiterOptions"/>.
    /// </summary>
    public sealed class RedisTokenBucketRateLimiterOptions : RedisRateLimiterOptions
    {
        /// <summary>
        /// Maximum number of permits that can be leased concurrently.
        /// Must be set to a value > 0 by the time these options are passed to the constructor of <see cref="RedisTokenBucketRateLimiter"/>.
        /// </summary>
        public int PermitLimit { get; set; }
    }
}
