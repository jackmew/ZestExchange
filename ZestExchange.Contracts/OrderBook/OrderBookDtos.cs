using Orleans;

namespace ZestExchange.Contracts.OrderBook;

[GenerateSerializer]
public record GetOrderBookRequest(
    [property: Id(0)] string Symbol,
    [property: Id(1)] int Depth = 10);

[GenerateSerializer]
public record GetOrderBookResponse(
    [property: Id(0)] string Symbol,
    [property: Id(1)] List<PriceLevelDto> Bids,
    [property: Id(2)] List<PriceLevelDto> Asks,
    [property: Id(3)] DateTime Timestamp);

[GenerateSerializer]
public record PriceLevelDto(
    [property: Id(0)] decimal Price,
    [property: Id(1)] decimal TotalQuantity);
