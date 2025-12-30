using Orleans;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;
using ZestExchange.Contracts.OrderBook;
using ZestExchange.Silo.Domain.OrderBook;

namespace ZestExchange.Silo.Grains;

/// <summary>
/// Orleans Grain 實作 - 撮合引擎
///
/// 每個交易對 (Symbol) 一個 Grain 實例
/// Orleans 保證同一時間只有一個執行緒執行 = Lock-free
/// </summary>
public class MatchingEngineGrain : Grain, IMatchingEngineGrain
{
    private readonly ILogger<MatchingEngineGrain> _logger;
    private OrderBookEngine _orderBook = null!;
    private string _symbol = null!;

    public MatchingEngineGrain(ILogger<MatchingEngineGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Grain Key = Symbol (e.g., "BTC-USDT")
        _symbol = this.GetPrimaryKeyString();
        _orderBook = new OrderBookEngine(_symbol);

        _logger.LogInformation("MatchingEngineGrain activated for {Symbol}", _symbol);

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        _logger.LogInformation(
            "PlaceOrder: {Symbol} {Side} {Type} Price={Price} Qty={Quantity}",
            _symbol, request.Side, request.Type, request.Price, request.Quantity);

        var (order, trades) = _orderBook.PlaceOrder(
            request.Side,
            request.Type,
            request.Price,
            request.Quantity);

        if (trades.Count > 0)
        {
            _logger.LogInformation(
                "Order {OrderId} matched {TradeCount} trades",
                order.Id, trades.Count);
        }

        return Task.FromResult(new PlaceOrderResponse(
            OrderId: order.Id,
            Status: order.Status,
            Message: trades.Count > 0
                ? $"Matched {trades.Count} trade(s)"
                : "Order placed in book"));
    }

    public Task<CancelOrderResponse> CancelOrderAsync(Guid orderId)
    {
        _logger.LogInformation("CancelOrder: {OrderId}", orderId);

        var success = _orderBook.CancelOrder(orderId);

        return Task.FromResult(new CancelOrderResponse(
            OrderId: orderId,
            Success: success,
            Message: success ? "Order cancelled" : "Order not found"));
    }

    public Task<GetOrderResponse?> GetOrderAsync(Guid orderId)
    {
        var order = _orderBook.GetOrder(orderId);

        if (order == null)
        {
            return Task.FromResult<GetOrderResponse?>(null);
        }

        return Task.FromResult<GetOrderResponse?>(new GetOrderResponse(
            OrderId: order.Id,
            Symbol: order.Symbol,
            Side: order.Side,
            Type: order.Type,
            Price: order.Price,
            Quantity: order.Quantity,
            FilledQuantity: order.FilledQuantity,
            Status: order.Status,
            CreatedAt: order.CreatedAt));
    }

    public Task<GetOrderBookResponse> GetOrderBookAsync(int depth = 10)
    {
        return Task.FromResult(_orderBook.GetSnapshot(depth));
    }
}
