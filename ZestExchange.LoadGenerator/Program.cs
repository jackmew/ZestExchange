using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZestExchange.LoadGenerator;

var builder = Host.CreateApplicationBuilder(args);

// 1. Add Service Defaults (OpenTelemetry, etc.)
builder.AddServiceDefaults();

// 2. Add Orleans Client
builder.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
});

// 3. Register Worker
builder.Services.AddHostedService<OrderGeneratorWorker>();

var host = builder.Build();
host.Run();