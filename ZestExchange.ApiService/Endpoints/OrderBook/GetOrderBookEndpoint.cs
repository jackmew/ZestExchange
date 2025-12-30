using FastEndpoints;
using Orleans;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.OrderBook;

namespace ZestExchange.ApiService.Endpoints.OrderBook;

public class GetOrderBookEndpoint : Endpoint<GetOrderBookRequest, GetOrderBookResponse>
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<GetOrderBookEndpoint> _logger;

    public GetOrderBookEndpoint(IClusterClient clusterClient, ILogger<GetOrderBookEndpoint> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/orderbook/{Symbol}");
        AllowAnonymous();
        Tags("OrderBook");
    }

    public override async Task HandleAsync(GetOrderBookRequest req, CancellationToken ct)
    {
        _logger.LogInformation("GetOrderBook: {Symbol} Depth={Depth}", req.Symbol, req.Depth);

        var grain = _clusterClient.GetGrain<IMatchingEngineGrain>(req.Symbol);
        Response = await grain.GetOrderBookAsync(req.Depth);
    }
}
