namespace RedisRateLimiting.Sample
{
    /// <summary>
    /// A custom implementation of RedisFixedWindowRateLimiter that adds custom metadata for Polly pipeline integration.
    /// </summary>
    public class PollyFixedWindowRateLimiter<TKey>(TKey partitionKey, RedisFixedWindowRateLimiterOptions options)
        : RedisFixedWindowRateLimiter<TKey>(partitionKey, options)
    {
        protected override void AddCustomMetadata(FixedWindowLeaseContext context)
        {
            if (context.RetryAfter.HasValue)
            {
                context.AddCustomMetadata("RETRY_AFTER", context.RetryAfter.Value);
            }
        }
    }
}