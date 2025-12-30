using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using ZestExchange.LoadGenerator;

var builder = Host.CreateApplicationBuilder(args);

// 1. Add Service Defaults (OpenTelemetry, etc.)
builder.AddServiceDefaults();

// 2. Add Orleans Client
builder.UseOrleansClient(client =>
{
    var connectionString = builder.Configuration.GetConnectionString("redis");

    if (string.IsNullOrEmpty(connectionString))
    {
        client.UseLocalhostClustering();
    }
    else
    {
        client.UseRedisClustering(options => options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString));
    }
});

// 3. Register Worker
builder.Services.AddHostedService<OrderGeneratorWorker>();

var host = builder.Build();
host.Run();