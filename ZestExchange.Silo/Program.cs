var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (OpenTelemetry, Health Checks)
builder.AddServiceDefaults();

// Configure Orleans Silo
builder.UseOrleans(siloBuilder =>
{
    // 開發環境：使用 Localhost Clustering
    siloBuilder.UseLocalhostClustering();

    // 記憶體儲存 (MVP 用，生產環境換 Redis) - Default - 用來儲存 Grain State（如果有 [PersistentState] 的話）
    siloBuilder.AddMemoryGrainStorage("Default");

    // PubSub storage required for Orleans Streams - PubSubStore - Orleans Streams 內部使用，追蹤 "誰訂閱了哪個 Stream"
    siloBuilder.AddMemoryGrainStorage("PubSubStore");

    // Memory Stream Provider for real-time OrderBook updates
    // 生產環境可換成 Azure Event Hubs 或 Kafka
    siloBuilder.AddMemoryStreams("OrderBookProvider");
});

var host = builder.Build();
host.Run();
