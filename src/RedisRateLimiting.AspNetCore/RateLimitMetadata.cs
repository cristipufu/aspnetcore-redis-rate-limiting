using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace RedisRateLimiting.AspNetCore;

public static class RateLimitMetadata
{
    /// <summary>
    /// Sets the default rate limit headers.
    /// </summary>
    public static Func<HttpContext, RateLimitLease, CancellationToken, ValueTask> OnRejected { get; } = (httpContext, lease, token) =>
    {
        httpContext.Response.StatusCode = 429;

        if (lease.TryGetMetadata(RateLimitMetadataName.Limit, out var limit))
        {
            httpContext.Response.Headers[RateLimitHeaders.Limit] = limit;
        }

        if (lease.TryGetMetadata(RateLimitMetadataName.Remaining, out var remaining))
        {
            httpContext.Response.Headers[RateLimitHeaders.Remaining] = remaining.ToString();
        }

        if (lease.TryGetMetadata(RateLimitMetadataName.Reset, out var reset))
        {
            httpContext.Response.Headers[RateLimitHeaders.Reset] = reset.ToString();
        }

        if (lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers[RateLimitHeaders.RetryAfter] = retryAfter.ToString();
        }

        return ValueTask.CompletedTask;
    };
}
