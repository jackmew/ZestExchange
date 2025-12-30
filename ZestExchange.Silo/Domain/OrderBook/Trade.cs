namespace ZestExchange.Silo.Domain.OrderBook;

// 成交記錄
public record Trade(
    Guid TradeId,
    Guid MakerOrderId,
    Guid TakerOrderId,
    decimal Price,
    decimal Quantity,
    DateTime Timestamp);
