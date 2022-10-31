using System;

namespace RedisRateLimiting
{
    /// <summary>
    /// Options to specify the behavior of a <see cref="RedisFixedWindowRateLimiter"/>.
    /// </summary>
    public sealed class RedisFixedWindowRateLimiterOptions : RedisRateLimiterOptions
    {
        /// <summary>
        /// Specifies the time window that takes in the requests.
        /// Must be set to a value greater than <see cref="TimeSpan.Zero" /> by the time these options are passed to the constructor of <see cref="RedisFixedWindowRateLimiter"/>.
        /// </summary>
        public TimeSpan Window { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Maximum number of permit counters that can be allowed in a window.
        /// Must be set to a value > 0 by the time these options are passed to the constructor of <see cref="RedisFixedWindowRateLimiter"/>.
        /// </summary>
        public int PermitLimit { get; set; }
    }
}
