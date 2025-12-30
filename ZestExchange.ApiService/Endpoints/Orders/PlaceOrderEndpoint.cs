using FastEndpoints;
using Orleans;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.ApiService.Endpoints.Orders;

public class PlaceOrderEndpoint : Endpoint<PlaceOrderRequest, PlaceOrderResponse>
{
    private readonly IClusterClient _clusterClient; // (DI 注入) Orleans 提供的「連接器」
    private readonly ILogger<PlaceOrderEndpoint> _logger;

    public PlaceOrderEndpoint(IClusterClient clusterClient, ILogger<PlaceOrderEndpoint> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/orders");
        AllowAnonymous();
        Tags("Orders");
    }

    public override async Task HandleAsync(PlaceOrderRequest req, CancellationToken ct)
    {
        _logger.LogInformation(
            "PlaceOrder: {Symbol} {Side} {Type} Price={Price} Qty={Quantity}",
            req.Symbol, req.Side, req.Type, req.Price, req.Quantity);

        // 取得該交易對的 Grain
        var grain = _clusterClient.GetGrain<IMatchingEngineGrain>(req.Symbol);

        // 呼叫 Grain 下單
        Response = await grain.PlaceOrderAsync(req);
    }
}
