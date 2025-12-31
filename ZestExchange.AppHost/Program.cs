var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for Orleans Clustering and Persistence
var redis = builder.AddRedis("redis");

// Add PostgreSQL for Trade History (Persistent Storage)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .PublishAsAzurePostgresFlexibleServer();
var db = postgres.AddDatabase("exchangedb");

// Orleans Silo (撮合引擎) - 不需要 HTTP endpoints
var silo = builder.AddProject<Projects.ZestExchange_Silo>("silo")
    .WithReference(redis)
    .WithReference(db);

// API Service (透過 localhost clustering 連接 Orleans)
var apiService = builder.AddProject<Projects.ZestExchange_ApiService>("apiservice")
    .WithReference(silo)
    .WithReference(redis);

// Web Frontend
builder.AddProject<Projects.ZestExchange_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(redis)
    .WithReference(db);

// Load Generator (Simulation)
builder.AddProject<Projects.ZestExchange_LoadGenerator>("load-btc")
    .WithEnvironment("LOAD_GEN_SYMBOL", "BTC-USDT")
    .WithEnvironment("LOAD_GEN_START_PRICE", "50000")
    .WithEnvironment("LOAD_GEN_USERS", "3")
    .WithEnvironment("LOAD_GEN_INTERVAL_MS", "300")
    .WithReference(redis);

builder.AddProject<Projects.ZestExchange_LoadGenerator>("load-eth")
    .WithEnvironment("LOAD_GEN_SYMBOL", "ETH-USDT")
    .WithEnvironment("LOAD_GEN_START_PRICE", "3000")
    .WithEnvironment("LOAD_GEN_USERS", "2")
    .WithEnvironment("LOAD_GEN_INTERVAL_MS", "500")
    .WithReference(redis);

builder.Build().Run();
