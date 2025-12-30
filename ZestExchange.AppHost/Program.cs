var builder = DistributedApplication.CreateBuilder(args);

// Orleans Silo (撮合引擎) - 不需要 HTTP endpoints
var silo = builder.AddProject<Projects.ZestExchange_Silo>("silo");

// API Service (透過 localhost clustering 連接 Orleans)
var apiService = builder.AddProject<Projects.ZestExchange_ApiService>("apiservice");

// Web Frontend
builder.AddProject<Projects.ZestExchange_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
