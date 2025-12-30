using FastEndpoints;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class GetOrderEndpoint : Endpoint<GetOrderRequest, GetOrderResponse>
{
    private readonly ILogger<GetOrderEndpoint> _logger;

    public GetOrderEndpoint(ILogger<GetOrderEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/orders/{OrderId}");
        AllowAnonymous();
        Tags("Orders");
    }

    public override Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation("GetOrder: {OrderId}", req.OrderId);

        // Mock implementation - return fake order data
        Response = new GetOrderResponse(
            OrderId: req.OrderId,
            Symbol: "BTC-USDT",
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            Price: 50000m,
            Quantity: 1.5m,
            FilledQuantity: 0.5m,
            Status: OrderStatus.PartiallyFilled,
            CreatedAt: DateTime.UtcNow.AddMinutes(-5));

        return Task.CompletedTask;
    }
}
