namespace ZestExchange.Contracts.Orders;

// Place Order
public record PlaceOrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Price,
    decimal Quantity);

public record PlaceOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    string? Message = null);

// Cancel Order
public record CancelOrderRequest(Guid OrderId);

public record CancelOrderResponse(
    Guid OrderId,
    bool Success,
    string? Message = null);

// Get Single Order
public record GetOrderRequest(Guid OrderId);

public record GetOrderResponse(
    Guid OrderId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    decimal FilledQuantity,
    OrderStatus Status,
    DateTime CreatedAt);

// Get Orders (List)
public record GetOrdersRequest(
    string? Symbol = null,
    OrderStatus? Status = null,
    int Limit = 50);

public record GetOrdersResponse(List<OrderSummary> Orders);

public record OrderSummary(
    Guid OrderId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    decimal FilledQuantity,
    OrderStatus Status,
    DateTime CreatedAt);
