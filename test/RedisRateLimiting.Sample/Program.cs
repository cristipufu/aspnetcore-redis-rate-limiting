using Microsoft.Extensions.Logging.Abstractions;
using RedisRateLimiting;
using RedisRateLimiting.Sample;
using RedisRateLimiting.Sample.Samples;
using StackExchange.Redis;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var redisOptions = ConfigurationOptions.Parse(",ssl=True,abortConnect=False");
var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => connectionMultiplexer);

builder.Services.AddRateLimiter(options =>
{
    options.AddRedisConcurrencyLimiter("demo_concurrency", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 2;
    });

    options.AddRedisTokenBucketLimiter("demo_token_bucket", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.TokenLimit = 2;
        opt.TokensPerPeriod = 1;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(2);
    });

    options.AddRedisFixedWindowLimiter("demo_fixed_window", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 1;
        opt.Window = TimeSpan.FromSeconds(2);
    });

    options.AddRedisSlidingWindowLimiter("demo_sliding_window", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 1;
        opt.Window = TimeSpan.FromSeconds(2);
    });

    options.AddPolicy<string, ClientIdRateLimiterPolicy>("demo_client_id");

    options.AddPolicy("demo_client_id2", new ClientIdRateLimiterPolicy(connectionMultiplexer, NullLogger<ClientIdRateLimiterPolicy>.Instance));

    options.OnRejected = (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;

        if (context.Lease.TryGetMetadata(RateLimitMetadataName.Limit, out var limit))
        {
            context.HttpContext.Response.Headers[RateLimitHeaders.Limit] = limit;
        }

        if (context.Lease.TryGetMetadata(RateLimitMetadataName.Remaining, out var remaining))
        {
            context.HttpContext.Response.Headers[RateLimitHeaders.Remaining] = remaining.ToString();
        }

        if (context.Lease.TryGetMetadata(RateLimitMetadataName.Reset, out var reset))
        {
            context.HttpContext.Response.Headers[RateLimitHeaders.Reset] = reset.ToString();
        }

        if (context.Lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers[RateLimitHeaders.RetryAfter] = retryAfter.ToString();
        }

        return ValueTask.CompletedTask;
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();



// Hack: make the implicit Program class public so test projects can access it
public partial class Program { }