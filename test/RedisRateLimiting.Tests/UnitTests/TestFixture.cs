using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace RedisRateLimiting.Tests.UnitTests
{
    public class TestFixture : IDisposable
    {
        public readonly IConfiguration Configuration;
        public readonly IConnectionMultiplexer ConnectionMultiplexer;
        public Func<IConnectionMultiplexer> ConnectionMultiplexerFactory;

        public TestFixture()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, false)
                .AddEnvironmentVariables()
                .Build();

            var redisOptions = ConfigurationOptions.Parse(Configuration.GetConnectionString("Redis"));
            ConnectionMultiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions);

            ConnectionMultiplexerFactory = () => ConnectionMultiplexer;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ConnectionMultiplexer?.Dispose();
            }
        }
    }
}
