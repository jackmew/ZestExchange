using Orleans;
using ZestExchange.Contracts.OrderBook;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.Contracts.Events;

/// <summary>
/// Event published when OrderBook changes (new order, cancel, match)
/// Used by Orleans Streams for real-time updates
/// </summary>
[GenerateSerializer]
public record OrderBookUpdated(
    [property: Id(0)] string Symbol,
    [property: Id(1)] List<PriceLevelDto> Bids,
    [property: Id(2)] List<PriceLevelDto> Asks,
    [property: Id(3)] DateTime Timestamp);

/// <summary>
/// Event published when a trade occurs
/// </summary>
[GenerateSerializer]
public record TradeOccurred(
    [property: Id(0)] string Symbol,
    [property: Id(1)] decimal Price,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] OrderSide TakerSide, // Was it a Buy or Sell that triggered the trade?
    [property: Id(4)] DateTime Timestamp);
