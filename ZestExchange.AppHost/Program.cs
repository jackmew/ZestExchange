var builder = DistributedApplication.CreateBuilder(args);

// Orleans Silo (撮合引擎)
var silo = builder.AddProject<Projects.ZestExchange_Silo>("silo");

// API Service (連接 Orleans)
var apiService = builder.AddProject<Projects.ZestExchange_ApiService>("apiservice")
    .WithReference(silo);

// Web Frontend
builder.AddProject<Projects.ZestExchange_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
