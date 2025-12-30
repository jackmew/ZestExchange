var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.ZestExchange_ApiService>("apiservice");

builder.AddProject<Projects.ZestExchange_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
