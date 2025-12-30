using ZestExchange.Contracts.Orders;

namespace ZestExchange.Silo.Domain.OrderBook;
// 訂單實體
public class Order
{
    public Guid Id { get; }
    public string Symbol { get; }
    public OrderSide Side { get; }
    public OrderType Type { get; }
    public decimal Price { get; }
    public decimal Quantity { get; private set; }
    public decimal FilledQuantity { get; private set; }
    public decimal RemainingQuantity => Quantity - FilledQuantity;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; }

    public Order(
        Guid id,
        string symbol,
        OrderSide side,
        OrderType type,
        decimal price,
        decimal quantity)
    {
        Id = id;
        Symbol = symbol;
        Side = side;
        Type = type;
        Price = price;
        Quantity = quantity;
        FilledQuantity = 0;
        Status = OrderStatus.New;
        CreatedAt = DateTime.UtcNow;
    }

    public void Fill(decimal quantity)
    {
        FilledQuantity += quantity;
        Status = FilledQuantity >= Quantity
            ? OrderStatus.Filled
            : OrderStatus.PartiallyFilled;
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
    }
}
