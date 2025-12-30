using Orleans;

namespace ZestExchange.Contracts.Orders;

// Place Order
[GenerateSerializer]
public record PlaceOrderRequest(
    [property: Id(0)] string Symbol,
    [property: Id(1)] OrderSide Side,
    [property: Id(2)] OrderType Type,
    [property: Id(3)] decimal Price,
    [property: Id(4)] decimal Quantity);

[GenerateSerializer]
public record PlaceOrderResponse(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] OrderStatus Status,
    [property: Id(2)] string? Message = null);

// Cancel Order
[GenerateSerializer]
public record CancelOrderRequest(
    [property: Id(0)] string Symbol,
    [property: Id(1)] Guid OrderId);

[GenerateSerializer]
public record CancelOrderResponse(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] bool Success,
    [property: Id(2)] string? Message = null);

// Get Single Order
[GenerateSerializer]
public record GetOrderRequest(
    [property: Id(0)] string Symbol,
    [property: Id(1)] Guid OrderId);

[GenerateSerializer]
public record GetOrderResponse(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Symbol,
    [property: Id(2)] OrderSide Side,
    [property: Id(3)] OrderType Type,
    [property: Id(4)] decimal Price,
    [property: Id(5)] decimal Quantity,
    [property: Id(6)] decimal FilledQuantity,
    [property: Id(7)] OrderStatus Status,
    [property: Id(8)] DateTime CreatedAt);

// Get Orders (List)
[GenerateSerializer]
public record GetOrdersRequest(
    [property: Id(0)] string? Symbol = null,
    [property: Id(1)] OrderStatus? Status = null,
    [property: Id(2)] int Limit = 50);

[GenerateSerializer]
public record GetOrdersResponse(
    [property: Id(0)] List<OrderSummary> Orders);

[GenerateSerializer]
public record OrderSummary(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string Symbol,
    [property: Id(2)] OrderSide Side,
    [property: Id(3)] OrderType Type,
    [property: Id(4)] decimal Price,
    [property: Id(5)] decimal Quantity,
    [property: Id(6)] decimal FilledQuantity,
    [property: Id(7)] OrderStatus Status,
    [property: Id(8)] DateTime CreatedAt);
