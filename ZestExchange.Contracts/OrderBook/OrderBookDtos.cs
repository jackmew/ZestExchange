namespace ZestExchange.Contracts.OrderBook;

public record GetOrderBookRequest(string Symbol, int Depth = 10);

public record GetOrderBookResponse(
    string Symbol,
    List<PriceLevelDto> Bids,
    List<PriceLevelDto> Asks,
    DateTime Timestamp);

public record PriceLevelDto(decimal Price, decimal TotalQuantity);
