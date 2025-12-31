using StackExchange.Redis;
using ZestExchange.Repository;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (OpenTelemetry, Health Checks)
builder.AddServiceDefaults();

// Add Repository (PostgreSQL)
builder.Services.AddZestRepository(builder.Configuration);

// Configure Orleans Silo
builder.UseOrleans(siloBuilder =>
{
    var connectionString = builder.Configuration.GetConnectionString("redis");

    if (string.IsNullOrEmpty(connectionString))
    {
        // 開發環境：使用 Localhost Clustering
        siloBuilder.UseLocalhostClustering();
        siloBuilder.AddMemoryGrainStorage("Default");
        siloBuilder.AddMemoryGrainStorage("PubSubStore");
    }
    else
    {
        // 生產環境或有 Docker Redis 時：使用 Redis
        siloBuilder.UseRedisClustering(options => options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString));
        siloBuilder.AddRedisGrainStorage("Default", options => options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString));
        siloBuilder.AddRedisGrainStorage("PubSubStore", options => options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString));
    }

    // Memory Stream Provider for real-time OrderBook updates
    // 注意：即使 Clustering 用 Redis，Stream 也可以暫時維持 Memory，只要 Producer/Consumer 在同一個 Silo 或透過 PubSubStore 協調
    siloBuilder.AddMemoryStreams("OrderBookProvider");
});

var host = builder.Build();
host.Run();
