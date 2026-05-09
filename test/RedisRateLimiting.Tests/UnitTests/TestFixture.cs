using Microsoft.Extensions.Configuration;

using StackExchange.Redis;

namespace RedisRateLimiting.Tests.UnitTests;

public class TestFixture
{
    public readonly IConfiguration Configuration;
    public readonly ConfigurationOptions RedisOptions;

    public TestFixture()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, false)
            .AddEnvironmentVariables()
            .Build();

        RedisOptions = ConfigurationOptions.Parse(Configuration.GetConnectionString("Redis"));
    }

    public IConnectionMultiplexer ConnectionMultiplexerFactory()
    {
        // Because the RateLimiters use a Factory pattern to create the IConnectionMultiplexer,
        // they should be responsible for disposal.
        // Ergo the implementation of ConnectionMultiplexerFactory must return a unique instance
        // of the ConnectionMultiplexer on every call. Inefficient for the Unit Tests, but
        // inconsequential in real-world scenarios where the RateLimiter itself will only be
        // instantiated once.
        return StackExchange.Redis.ConnectionMultiplexer.Connect(RedisOptions);
    }
}
