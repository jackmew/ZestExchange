using FastEndpoints;
using Orleans;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class CancelOrderEndpoint : Endpoint<CancelOrderRequest, CancelOrderResponse>
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CancelOrderEndpoint> _logger;

    public CancelOrderEndpoint(IClusterClient clusterClient, ILogger<CancelOrderEndpoint> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Delete("/api/orders/{Symbol}/{OrderId}");
        AllowAnonymous();
        Tags("Orders");
    }

    public override async Task HandleAsync(CancelOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation("CancelOrder: {Symbol} {OrderId}", req.Symbol, req.OrderId);

        var grain = _clusterClient.GetGrain<IMatchingEngineGrain>(req.Symbol);
        Response = await grain.CancelOrderAsync(req.OrderId);
    }
}
