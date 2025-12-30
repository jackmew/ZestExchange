using FastEndpoints;

namespace ZestExchange.ApiService.Endpoints;

public class HealthCheckEndpoint : EndpointWithoutRequest<HealthCheckResponse>
{
    private readonly ILogger<HealthCheckEndpoint> _logger;

    public HealthCheckEndpoint(ILogger<HealthCheckEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
        Tags("Health");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Health check requested");
        Response = new HealthCheckResponse("Healthy", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}

public record HealthCheckResponse(string Status, DateTime Timestamp);
