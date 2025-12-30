using FastEndpoints;

namespace ZestExchange.ApiService.Endpoints.OrderBook;

public class GetOrderBookEndpoint : Endpoint<GetOrderBookRequest, GetOrderBookResponse>
{
    private readonly ILogger<GetOrderBookEndpoint> _logger;

    public GetOrderBookEndpoint(ILogger<GetOrderBookEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/orderbook/{Symbol}");
        AllowAnonymous();
        Tags("OrderBook");
    }

    public override Task HandleAsync(GetOrderBookRequest req, CancellationToken ct)
    {
        _logger.LogInformation("GetOrderBook: {Symbol} Depth={Depth}", req.Symbol, req.Depth);

        // Mock implementation - return fake orderbook data
        var bids = new List<PriceLevelDto>
        {
            new(49900m, 5.5m),
            new(49800m, 3.2m),
            new(49700m, 8.1m),
            new(49600m, 2.4m),
            new(49500m, 6.7m)
        };

        var asks = new List<PriceLevelDto>
        {
            new(50100m, 2.1m),
            new(50200m, 4.8m),
            new(50300m, 1.9m),
            new(50400m, 7.3m),
            new(50500m, 3.5m)
        };

        Response = new GetOrderBookResponse(
            Symbol: req.Symbol,
            Bids: bids.Take(req.Depth).ToList(),
            Asks: asks.Take(req.Depth).ToList(),
            Timestamp: DateTime.UtcNow);

        return Task.CompletedTask;
    }
}

public record GetOrderBookRequest(string Symbol, int Depth = 10);

public record GetOrderBookResponse(
    string Symbol,
    List<PriceLevelDto> Bids,
    List<PriceLevelDto> Asks,
    DateTime Timestamp);

public record PriceLevelDto(decimal Price, decimal TotalQuantity);
