using RedisRateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisOptions = ConfigurationOptions.Parse("rrl.redis.cache.windows.net:6380,password=lcWqmZ3JK6OYIAKvzahLdv25AsDY9Eq8EAzCaBZJkuM=,ssl=True,abortConnect=False");
var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddRateLimiter(options =>
{
    options.AddRedisConcurrencyLimiter("demo_concurrency", (opt) =>
    {
        opt.ConnectionMultiplexer = connectionMultiplexer;
        opt.PermitLimit = 2;
    });

    //options.AddRedisTokenBucketLimiter("demo_token_bucket", (opt) =>
    //{
    //    opt.ConnectionMultiplexer = connectionMultiplexer;
    //    opt.PermitLimit = 1;
    //});

    options.AddRedisFixedWindowLimiter("demo_fixed_window", (opt) =>
    {
        opt.ConnectionMultiplexer = connectionMultiplexer;
        opt.PermitLimit = 1;
        opt.Window = TimeSpan.FromSeconds(2);
    });

    options.OnRejected = (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
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