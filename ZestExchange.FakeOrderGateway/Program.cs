using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZestExchange.FakeOrderGateway;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (OpenTelemetry, Health Checks)
builder.AddServiceDefaults();

// Configure Orleans Client
builder.UseOrleansClient(clientBuilder =>
{
    clientBuilder.UseLocalhostClustering();
});

// Bind configuration
builder.Services.Configure<LoadDriverConfig>(
    builder.Configuration.GetSection("LoadDriver"));

// Add hosted service to run the load driver
builder.Services.AddHostedService<LoadDriverHostedService>();

var host = builder.Build();
host.Run();

/// <summary>
/// Hosted service that runs the LoadDriver
/// </summary>
public class LoadDriverHostedService : BackgroundService
{
    private readonly LoadDriver _loadDriver;
    private readonly ILogger<LoadDriverHostedService> _logger;

    public LoadDriverHostedService(
        IClusterClient client,
        IOptions<LoadDriverConfig> config,
        ILoggerFactory loggerFactory)
    {
        _loadDriver = new LoadDriver(client, config.Value, loggerFactory.CreateLogger<LoadDriver>());
        _logger = loggerFactory.CreateLogger<LoadDriverHostedService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FakeOrderGateway starting...");

        // Wait a bit for Orleans cluster to be ready
        await Task.Delay(2000, stoppingToken);

        await _loadDriver.DriveLoadAsync(stoppingToken);
    }
}
