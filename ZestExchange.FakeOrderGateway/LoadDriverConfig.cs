namespace ZestExchange.FakeOrderGateway;

/// <summary>
/// Configuration for the load driver
/// </summary>
public class LoadDriverConfig
{
    /// <summary>
    /// Orders per second to generate
    /// </summary>
    public int OrdersPerSecond { get; set; } = 10;

    /// <summary>
    /// Trading symbol (e.g., "BTC-USDT")
    /// </summary>
    public string Symbol { get; set; } = "BTC-USDT";

    /// <summary>
    /// Mid price for the market simulation
    /// </summary>
    public decimal MidPrice { get; set; } = 50000m;

    /// <summary>
    /// Spread percentage from mid price (e.g., 0.1 = 0.1%)
    /// </summary>
    public decimal SpreadPercent { get; set; } = 0.1m;

    /// <summary>
    /// Min order quantity
    /// </summary>
    public decimal MinQuantity { get; set; } = 0.1m;

    /// <summary>
    /// Max order quantity
    /// </summary>
    public decimal MaxQuantity { get; set; } = 10m;

    /// <summary>
    /// Probability of placing a marketable order (crosses the spread, triggers match)
    /// </summary>
    public double MarketableProbability { get; set; } = 0.3;
}
