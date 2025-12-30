var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (OpenTelemetry, Health Checks)
builder.AddServiceDefaults();

// Configure Orleans Silo
builder.UseOrleans(siloBuilder =>
{
    // 開發環境：使用 Localhost Clustering
    siloBuilder.UseLocalhostClustering();

    // 記憶體儲存 (MVP 用，生產環境換 Redis)
    siloBuilder.AddMemoryGrainStorage("Default");
});

var host = builder.Build();
host.Run();
