using Orleans;
using ZestExchange.Contracts.Orders;
using ZestExchange.Contracts.OrderBook;

namespace ZestExchange.Contracts.Grains;

/// <summary>
/// Orleans Grain Interface for Matching Engine
///
/// Key: Symbol (e.g., "BTC-USDT")
/// Each trading pair has its own Grain instance = Lock-free matching
/// </summary>
public interface IMatchingEngineGrain : IGrainWithStringKey
{
    /// <summary>
    /// Place a new order
    /// </summary>
    Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request);

    /// <summary>
    /// Cancel an existing order
    /// </summary>
    Task<CancelOrderResponse> CancelOrderAsync(Guid orderId);

    /// <summary>
    /// Get order by ID
    /// </summary>
    Task<GetOrderResponse?> GetOrderAsync(Guid orderId);

    /// <summary>
    /// Get orderbook snapshot
    /// </summary>
    Task<GetOrderBookResponse> GetOrderBookAsync(int depth = 10);
}
