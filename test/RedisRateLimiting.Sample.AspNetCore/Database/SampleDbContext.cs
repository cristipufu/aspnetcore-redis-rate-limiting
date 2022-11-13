using Microsoft.EntityFrameworkCore;

namespace RedisRateLimiting.Sample.AspNetCore.Database
{
    #nullable disable
    public class SampleDbContext : DbContext
    {
        public string DbPath { get; }

        public SampleDbContext() : base()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);

            DbPath = Path.Join(path, "ratelimit.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RateLimit>().HasData(
                new RateLimit
                {
                    Id = 1,
                    PermitLimit = 1,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(1),
                },
                new RateLimit
                {
                    Id = 2,
                    PermitLimit = 2,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(1),
                },
                new RateLimit
                {
                    Id = 3,
                    PermitLimit = 3,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(1),
                });

            modelBuilder.Entity<Client>().HasData(
                new Client
                {
                    Id = 1,
                    RateLimitId = 1,
                    Identifier = "client1",
                },
                new Client
                {
                    Id = 2,
                    RateLimitId = 2,
                    Identifier = "client2",
                },
                new Client
                {
                    Id = 3,
                    RateLimitId = 3,
                    Identifier = "client3",
                });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");


        public DbSet<Client> Clients { get; set; }

        public DbSet<RateLimit> RateLimits { get; set; }
    }

    public class RateLimit
    {
        public long Id { get; set; }

        public int PermitLimit { get; set; }

        public int QueueLimit { get; set; }

        public TimeSpan Window { get; set; }

        public List<Client> Clients { get; set; }
    }

    public class Client
    {
        public long Id { get; set; }

        public string Identifier { get; set; }

        public long RateLimitId { get; set; }

        public RateLimit RateLimit { get; set; }
    }
}
