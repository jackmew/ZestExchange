using FastEndpoints;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class CancelOrderEndpoint : Endpoint<CancelOrderRequest, CancelOrderResponse>
{
    private readonly ILogger<CancelOrderEndpoint> _logger;

    public CancelOrderEndpoint(ILogger<CancelOrderEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Delete("/api/orders/{OrderId}");
        AllowAnonymous();
        Tags("Orders");
    }

    public override Task HandleAsync(CancelOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation("CancelOrder: {OrderId}", req.OrderId);

        // Mock implementation
        Response = new CancelOrderResponse(
            OrderId: req.OrderId,
            Success: true,
            Message: "Order cancelled successfully (mock)");

        return Task.CompletedTask;
    }
}
