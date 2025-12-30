using FastEndpoints;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class PlaceOrderEndpoint : Endpoint<PlaceOrderRequest, PlaceOrderResponse>
{
    private readonly ILogger<PlaceOrderEndpoint> _logger;

    public PlaceOrderEndpoint(ILogger<PlaceOrderEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/orders");
        AllowAnonymous();
        Tags("Orders");
    }

    public override Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation(
            "PlaceOrder: {Symbol} {Side} {Type} Price={Price} Qty={Quantity}",
            req.Symbol, req.Side, req.Type, req.Price, req.Quantity);

        // Mock implementation - generate a fake order ID
        var orderId = Guid.NewGuid();

        Response = new PlaceOrderResponse(
            OrderId: orderId,
            Status: OrderStatus.New,
            Message: "Order placed successfully (mock)");

        return Task.CompletedTask;
    }
}
