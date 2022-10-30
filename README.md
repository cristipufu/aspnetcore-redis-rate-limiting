# aspnetcore-redis-rate-limiting

[![NuGet](https://img.shields.io/nuget/v/RedisRateLimiting)](https://www.nuget.org/packages/RedisRateLimiting) 
[![GitHub](https://img.shields.io/github/license/cristipufu/aspnetcore-redis-rate-limiting)](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/LICENSE)

Set up a Redis backplane for Rate Limiting ASP.NET Core multi-node deployments. The library is created as an extension of the [built-in Rate Limiting support that's part of .NET 7](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/).


# install
```xml
PM> Install-Package RedisRateLimiting -Version 1.0.3
```

## snippets
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
