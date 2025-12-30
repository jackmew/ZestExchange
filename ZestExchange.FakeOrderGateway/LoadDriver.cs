using Microsoft.Extensions.Logging;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.FakeOrderGateway;

/// <summary>
/// Load driver that simulates realistic market order flow
/// </summary>
public class LoadDriver
{
    private readonly IClusterClient _client;
    private readonly LoadDriverConfig _config;
    private readonly ILogger<LoadDriver> _logger;
    private readonly Random _random = new();

    // Statistics
    private int _totalOrders;
    private int _matchedOrders;
    private int _pendingOrders;
    private DateTime _lastStatTime = DateTime.UtcNow;

    public LoadDriver(IClusterClient client, LoadDriverConfig config, ILogger<LoadDriver> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Run the load driver until cancellation
    /// </summary>
    public async Task DriveLoadAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting load driver: {OrdersPerSecond} orders/sec, Symbol={Symbol}, MidPrice={MidPrice}",
            _config.OrdersPerSecond, _config.Symbol, _config.MidPrice);

        var grain = _client.GetGrain<IMatchingEngineGrain>(_config.Symbol);
        var delayMs = 1000 / _config.OrdersPerSecond;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Generate and place order
                var order = GenerateOrder();
                var response = await grain.PlaceOrderAsync(order);

                // Update statistics
                _totalOrders++;
                if (response.Status == OrderStatus.Filled || response.Status == OrderStatus.PartiallyFilled)
                {
                    _matchedOrders++;
                }
                else
                {
                    _pendingOrders++;
                }

                // Log statistics every second
                LogStatistics();

                // Wait for next order
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogInformation("Load driver stopped. Total orders: {Total}", _totalOrders);
    }

    /// <summary>
    /// Generate a realistic market order
    /// </summary>
    private PlaceOrderRequest GenerateOrder()
    {
        var side = _random.NextDouble() > 0.5 ? OrderSide.Buy : OrderSide.Sell;
        var isMarketable = _random.NextDouble() < _config.MarketableProbability;

        // Calculate price based on side and whether it's marketable
        decimal price;
        var spreadAmount = _config.MidPrice * (_config.SpreadPercent / 100m);

        if (side == OrderSide.Buy)
        {
            if (isMarketable)
            {
                // Marketable buy: price above mid (crosses into asks)
                price = _config.MidPrice + RandomDecimal(0, spreadAmount * 2);
            }
            else
            {
                // Non-marketable buy: price below mid (rests in bids)
                price = _config.MidPrice - RandomDecimal(spreadAmount, spreadAmount * 3);
            }
        }
        else // Sell
        {
            if (isMarketable)
            {
                // Marketable sell: price below mid (crosses into bids)
                price = _config.MidPrice - RandomDecimal(0, spreadAmount * 2);
            }
            else
            {
                // Non-marketable sell: price above mid (rests in asks)
                price = _config.MidPrice + RandomDecimal(spreadAmount, spreadAmount * 3);
            }
        }

        // Round price to 2 decimal places
        price = Math.Round(price, 2);

        // Random quantity
        var quantity = RandomDecimal(_config.MinQuantity, _config.MaxQuantity);
        quantity = Math.Round(quantity, 4);

        return new PlaceOrderRequest(
            Symbol: _config.Symbol,
            Side: side,
            Type: OrderType.Limit,
            Price: price,
            Quantity: quantity);
    }

    private decimal RandomDecimal(decimal min, decimal max)
    {
        var range = (double)(max - min);
        return min + (decimal)(_random.NextDouble() * range);
    }

    private void LogStatistics()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastStatTime).TotalSeconds >= 1)
        {
            var elapsed = (now - _lastStatTime).TotalSeconds;
            var tps = _totalOrders / elapsed;

            _logger.LogInformation(
                "[STATS] TPS: {TPS:F1} | Total: {Total} | Matched: {Matched} | Pending: {Pending}",
                tps, _totalOrders, _matchedOrders, _pendingOrders);

            // Reset for next interval
            _totalOrders = 0;
            _matchedOrders = 0;
            _pendingOrders = 0;
            _lastStatTime = now;
        }
    }
}
