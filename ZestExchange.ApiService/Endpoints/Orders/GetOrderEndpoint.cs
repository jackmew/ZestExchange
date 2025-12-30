using FastEndpoints;
using Orleans;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class GetOrderEndpoint : Endpoint<GetOrderRequest, GetOrderResponse>
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<GetOrderEndpoint> _logger;

    public GetOrderEndpoint(IClusterClient clusterClient, ILogger<GetOrderEndpoint> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/orders/{Symbol}/{OrderId}");
        AllowAnonymous();
        Tags("Orders");
    }

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation("GetOrder: {Symbol} {OrderId}", req.Symbol, req.OrderId);

        var grain = _clusterClient.GetGrain<IMatchingEngineGrain>(req.Symbol);
        var order = await grain.GetOrderAsync(req.OrderId);

        if (order == null)
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        Response = order;
    }
}
