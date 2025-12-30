using Orleans;
using ZestExchange.Contracts.OrderBook;

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
