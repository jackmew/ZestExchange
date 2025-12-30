namespace ZestExchange.Contracts.Orders;

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Limit,
    Market
}

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}
