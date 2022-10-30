# aspnetcore-redis-rate-limiting
Redis extension for .NET 7.0 rate limiting 

```C#
var redisOptions = ConfigurationOptions.Parse(",ssl=True,abortConnect=False");
var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddRateLimiter(options =>
{
    options.AddRedisConcurrencyLimiter("demo_concurrency", (opt) =>
    {
        opt.ConnectionMultiplexer = connectionMultiplexer;
        opt.PermitLimit = 10;
    });

    options.AddRedisTokenBucketLimiter("demo_token_bucket", (opt) =>
    {
        opt.ConnectionMultiplexer = connectionMultiplexer;
        opt.PermitLimit = 10;
    });

    options.AddRedisFixedWindowLimiter("demo_fixed_window", (opt) =>
    {
        opt.ConnectionMultiplexer = connectionMultiplexer;
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromSeconds(2);
    });

    options.OnRejected = (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        return ValueTask.CompletedTask;
    };
});
```
