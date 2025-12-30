using FastEndpoints;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class GetOrdersEndpoint : Endpoint<GetOrdersRequest, GetOrdersResponse>
{
    private readonly ILogger<GetOrdersEndpoint> _logger;

    public GetOrdersEndpoint(ILogger<GetOrdersEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/orders");
        AllowAnonymous();
        Tags("Orders");
    }

    public override Task HandleAsync(GetOrdersRequest req, CancellationToken ct)
    {
        _logger.LogInformation(
            "GetOrders: Symbol={Symbol} Status={Status} Limit={Limit}",
            req.Symbol, req.Status, req.Limit);

        // Mock implementation - return fake orders
        var orders = new List<OrderSummary>
        {
            new(Guid.NewGuid(), "BTC-USDT", OrderSide.Buy, OrderType.Limit,
                50000m, 1.5m, 0.5m, OrderStatus.PartiallyFilled, DateTime.UtcNow.AddMinutes(-10)),
            new(Guid.NewGuid(), "BTC-USDT", OrderSide.Sell, OrderType.Limit,
                51000m, 2.0m, 0m, OrderStatus.New, DateTime.UtcNow.AddMinutes(-5)),
            new(Guid.NewGuid(), "ETH-USDT", OrderSide.Buy, OrderType.Market,
                0m, 10m, 10m, OrderStatus.Filled, DateTime.UtcNow.AddMinutes(-30))
        };

        // Apply filters
        var filtered = orders.AsEnumerable();
        if (!string.IsNullOrEmpty(req.Symbol))
            filtered = filtered.Where(o => o.Symbol == req.Symbol);
        if (req.Status.HasValue)
            filtered = filtered.Where(o => o.Status == req.Status.Value);

        Response = new GetOrdersResponse(filtered.Take(req.Limit).ToList());

        return Task.CompletedTask;
    }
}
