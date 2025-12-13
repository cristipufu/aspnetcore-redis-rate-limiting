using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting.Sample.AspNetCore.Database;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace RedisRateLimiting.Sample.Samples;

public class ClientIdRateLimiterPolicy(
    IConnectionMultiplexer connectionMultiplexer,
    IServiceProvider serviceProvider,
    ILogger<ClientIdRateLimiterPolicy> logger) : IRateLimiterPolicy<string>
{
    private readonly Func<OnRejectedContext, CancellationToken, ValueTask>? _onRejected = (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            return ValueTask.CompletedTask;
        };
    private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ClientIdRateLimiterPolicy> _logger = logger;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get => _onRejected; }

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var clientId = httpContext.Request.Headers["X-ClientId"].ToString();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var rateLimit = dbContext.Clients.Where(x => x.Identifier == clientId).Select(x => x.RateLimit).FirstOrDefault();

        var permitLimit = rateLimit?.PermitLimit ?? 1;

        _logger.LogInformation("Client: {clientId} PermitLimit: {permitLimit}", clientId, permitLimit);

        return RedisRateLimitPartition.GetConcurrencyRateLimiter(clientId, key => new RedisConcurrencyRateLimiterOptions
        {
            PermitLimit = permitLimit,
            QueueLimit = rateLimit?.QueueLimit ?? 0,
            ConnectionMultiplexerFactory = () => _connectionMultiplexer,
        });
    }
}