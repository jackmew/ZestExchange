var builder = DistributedApplication.CreateBuilder(args);

// Orleans Silo (撮合引擎) - 不需要 HTTP endpoints
var silo = builder.AddProject<Projects.ZestExchange_Silo>("silo");

// API Service (透過 localhost clustering 連接 Orleans)
var apiService = builder.AddProject<Projects.ZestExchange_ApiService>("apiservice");

// Web Frontend
builder.AddProject<Projects.ZestExchange_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

// Load Generator (Simulation)
builder.AddProject<Projects.ZestExchange_LoadGenerator>("loadgenerator")
    .WithEnvironment("LOAD_GEN_USERS", "5") // Simulate 5 concurrent users
    .WithEnvironment("LOAD_GEN_INTERVAL_MS", "200"); // 5 orders/sec per user ~= 25 TPS

builder.Build().Run();
