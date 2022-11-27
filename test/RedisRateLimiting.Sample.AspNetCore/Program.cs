using Microsoft.EntityFrameworkCore;
using RedisRateLimiting.AspNetCore;
using RedisRateLimiting.Sample.AspNetCore.Database;
using RedisRateLimiting.Sample.Samples;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SampleDbContext>();

var redisOptions = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => connectionMultiplexer);

builder.Services.AddRateLimiter(options =>
{
    options.AddRedisConcurrencyLimiter("demo_concurrency_queue", (opt) =>
    {
        opt.ConnectionMultiplexerFactory = () => connectionMultiplexer;
        opt.PermitLimit = 2;
        opt.QueueLimit = 3;
    });

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

    options.OnRejected = (context, ct) => RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, ct);
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


using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
dbContext.Database.Migrate();

app.Run();



// Hack: make the implicit Program class public so test projects can access it
public partial class Program { }