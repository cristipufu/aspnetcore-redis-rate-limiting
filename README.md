# aspnetcore-redis-rate-limiting


Set up a Redis backplane for Rate Limiting ASP.NET Core multi-node deployments. The library is build on top of the [built-in Rate Limiting support that's part of .NET 7](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/).

For more advanced use cases you can check out the [official documentation here](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-7.0).



# install

[![NuGet](https://img.shields.io/nuget/v/RedisRateLimiting)](https://www.nuget.org/packages/RedisRateLimiting) [![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fcristipufu%2Faspnetcore-redis-rate-limiting.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fcristipufu%2Faspnetcore-redis-rate-limiting?ref=badge_shield)

```xml
PM> Install-Package RedisRateLimiting
```
```
TargetFramework: net7.0

Dependencies:
StackExchange.Redis (>= 2.6.70)
System.Threading.RateLimiting (>= 7.0.0)
```


[![NuGet](https://img.shields.io/nuget/v/RedisRateLimiting.AspNetCore)](https://www.nuget.org/packages/RedisRateLimiting.AspNetCore) 
```xml
PM> Install-Package RedisRateLimiting.AspNetCore
```
```
TargetFramework: net7.0

Dependencies:
Microsoft.AspNetCore.RateLimiting (>= 7.0.0-rc.2.22476.2)
RedisRateLimiting (>= 1.0.6)
```

# strategies

## Concurrent Requests Rate Limiting

<br>

Concurrency Rate Limiter limits how many concurrent requests can access a resource. If your limit is 10, then 10 requests can access a resource at once and the 11th request will not be allowed. Once the first request completes, the number of allowed requests increases to 1, when the second request completes, the number increases to 2, etc.

Instead of "You can use our API 1000 times per second", this rate limiting strategy says "You can only have 20 API requests in progress at the same time".

<br>

![concurrency](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/docs/concurrency.png)

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

![fixed_window](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/docs/fixed_window.png)

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

## Sliding Window Rate Limiting

<br>

Unlike the Fixed Window Rate Limiter, which groups the requests into a bucket based on a very definitive time window, the Sliding Window Rate Limiter, restricts requests relative to the current request's timestamp. For example, if you have a 10 req/minute rate limiter, on a fixed window, you could encounter a case where the rate-limiter allows 20 requests during a one minute interval. This can happen if the first 10 requests are on the left side of the current window, and the next 10 requests are on the right side of the window, both having enough space in their respective buckets to be allowed through. If you send those same 20 requests through a Sliding Window Rate Limiter, if they are all sent during a one minute window, only 10 will make it through.

<br>

![fixed_window](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/docs/sliding_window.png)

<br>

You can use a new instance of the [RedisSlidingWindowRateLimiter](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/src/RedisRateLimiting/SlidingWindow/RedisSlidingWindowRateLimiter.cs) class or configure the predefined extension method:

```C#
builder.Services.AddRateLimiter(options =>
{
    options.AddRedisSlidingWindowLimiter("demo_sliding_window", (opt) =>
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

![token_bucket](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/docs/token_bucket.png)

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


[![GitHub](https://img.shields.io/github/license/cristipufu/aspnetcore-redis-rate-limiting)](https://github.com/cristipufu/aspnetcore-redis-rate-limiting/blob/master/LICENSE)


## License
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fcristipufu%2Faspnetcore-redis-rate-limiting.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fcristipufu%2Faspnetcore-redis-rate-limiting?ref=badge_large)