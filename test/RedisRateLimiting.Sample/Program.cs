using RedisRateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisOptions = ConfigurationOptions.Parse(",ssl=True,abortConnect=False");
var connectionMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

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