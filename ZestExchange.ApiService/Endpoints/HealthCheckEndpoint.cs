using FastEndpoints;

namespace ZestExchange.ApiService.Endpoints;

public class HealthCheckEndpoint : EndpointWithoutRequest<HealthCheckResponse>
{
    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = new HealthCheckResponse("Healthy", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}

public record HealthCheckResponse(string Status, DateTime Timestamp);
