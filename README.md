# aspnetcore-redis-rate-limiting

[![NuGet](https://img.shields.io/nuget/v/RedisRateLimiting)](https://www.nuget.org/packages/RedisRateLimiting) 
[![GitHub](https://img.shields.io/github/license/cristipufu/aspnetcore-redis-rate-limiting)](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/LICENSE)

Set up a Redis backplane for Rate Limiting ASP.NET Core multi-node deployments. The library is build on top of the [built-in Rate Limiting support that's part of .NET 7](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/).

For more advanced use cases you can check out the [official documentation here](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-7.0).



# install
```xml
PM> Install-Package RedisRateLimiting
```

# strategies

## Concurrent Requests Rate Limiting

<br>

Concurrency Rate Limiter limits how many concurrent requests can access a resource. If your limit is 10, then 10 requests can access a resource at once and the 11th request will not be allowed. Once the first request completes, the number of allowed requests increases to 1, when the second request completes, the number increases to 2, etc.

Instead of "You can use our API 1000 times per second", this rate limiting strategy says "You can only have 20 API requests in progress at the same time".

<br>

![concurrency](https://user-images.githubusercontent.com/3955285/199057204-a543669d-76b6-4c98-894d-9bb6dd843091.png)

<br>

You can use a new instance of the [RedisConcurrencyRateLimiter](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/src/RedisRateLimiting/Concurrency/RedisConcurrencyRateLimiter.cs) class or configure the predefined extension method:

```C#
builder.Services.AddRateLimiter(options =>
{
    options.AddRedisConcurrencyLimiter("demo_concurrency", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 5;
        // Queue requests when the limit is reached
        //opt.QueueLimit = 5 
    });
});
```

<br>

![concurrent_queuing_requests](https://user-images.githubusercontent.com/3955285/201516823-f0413ad7-de83-4393-acd7-a2f7c8c1e359.gif)

<br>

## Fixed Window Rate Limiting

<br>

The Fixed Window algorithm uses the concept of a window. The window is the amount of time that our limit is applied before we move on to the next window. In the Fixed Window strategy, moving to the next window means resetting the limit back to its starting point.

<br>

![fixed_window](https://user-images.githubusercontent.com/3955285/199057624-77a7fa4e-d247-473d-b595-d8733d58edd5.png)

<br>

You can use a new instance of the [RedisFixedWindowRateLimiter](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/src/RedisRateLimiting/FixedWindow/RedisFixedWindowRateLimiter.cs) class or configure the predefined extension method:

```C#
builder.Services.AddRateLimiter(options =>
{
    options.AddRedisFixedWindowLimiter("demo_fixed_window", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 1;
        opt.Window = TimeSpan.FromSeconds(2);
    });
});
```

<br>

## Token Bucket Rate Limiting

<br>

Token Bucket is an algorithm that derives its name from describing how it works. Imagine there is a bucket filled to the brim with tokens. When a request comes in, it takes a token and keeps it forever. After some consistent period of time, someone adds a pre-determined number of tokens back to the bucket, never adding more than the bucket can hold. If the bucket is empty, when a request comes in, the request is denied access to the resource.

<br>

![token_bucket](https://user-images.githubusercontent.com/3955285/199061414-e55b71c3-e4c5-4ee3-a342-dbe43c899152.png)

<br>

You can use a new instance of the [RedisTokenBucketRateLimiter](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/src/RedisRateLimiting/TokenBucket/RedisTokenBucketRateLimiter.cs) class or configure the predefined extension method:

```C#
builder.Services.AddRateLimiter(options =>
{
    options.AddRedisTokenBucketLimiter("demo_token_bucket", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.TokenLimit = 2;
        opt.TokensPerPeriod = 1;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(2);
    });
});
```

<br>

## snippets

These samples intentionally keep things simple for clarity.

- [Custom Rate Limiting Policies](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/wiki/Custom-Rate-Limiting-Policies)
- [Rate Limiting Headers](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/wiki/Rate-Limiting-Headers)

<br>

---

### MIT License

