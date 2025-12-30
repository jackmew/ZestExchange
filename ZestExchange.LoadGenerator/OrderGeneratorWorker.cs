using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.LoadGenerator;

public class OrderGeneratorWorker : BackgroundService
{
    private readonly IClusterClient _client;
    private readonly ILogger<OrderGeneratorWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly Meter _meter;
    private readonly Counter<long> _orderCounter;

    // Simulation State
    private const string DefaultSymbol = "BTC-USDT";
    private decimal _currentPrice; 
    private readonly Random _random = new();

    public OrderGeneratorWorker(
        IClusterClient client, 
        ILogger<OrderGeneratorWorker> logger, 
        IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        _configuration = configuration;
        
        // Load starting price from environment, default to 50000
        _currentPrice = _configuration.GetValue<decimal>("LOAD_GEN_START_PRICE", 50000m);

        // Metrics
        _meter = new Meter("ZestExchange.LoadGenerator");
        _orderCounter = _meter.CreateCounter<long>("orders_placed", description: "Total orders placed by generator");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Orleans Client to connect (Host.StartAsync handles this, but good to be safe)
        // actually BackgroundService starts after Host is started so Client is ready.

        var symbol = _configuration["LOAD_GEN_SYMBOL"] ?? DefaultSymbol;
        var intervalMs = _configuration.GetValue<int>("LOAD_GEN_INTERVAL_MS", 100); 
        var userCount = _configuration.GetValue<int>("LOAD_GEN_USERS", 1);

        _logger.LogInformation("🚀 Load Generator Started. Symbol: {Symbol}, Users: {Users}, Interval: {Interval}ms", 
            symbol, userCount, intervalMs);

        // Simple Random Walk & Order Generation Loop
        // simulating multiple users
        var tasks = new List<Task>();
        for (int i = 0; i < userCount; i++)
        {
            tasks.Add(RunUserLoop(i, symbol, intervalMs, stoppingToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task RunUserLoop(int userId, string symbol, int intervalMs, CancellationToken ct)
    {
        var grain = _client.GetGrain<IMatchingEngineGrain>(symbol);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 1. Update Market Price (Random Walk) - Shared State but it's fine for simulation
                UpdatePrice();

                // 2. Decide: Buy or Sell?
                var side = _random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;

                // 3. Determine Price (Around current price with spread)
                // Spread ~ 0.2%
                var spread = _currentPrice * 0.002m;
                var priceOffset = (decimal)(_random.NextDouble() * (double)spread);
                
                // Buy lower, Sell higher
                var price = side == OrderSide.Buy 
                    ? _currentPrice - priceOffset 
                    : _currentPrice + priceOffset;

                // Round to 2 decimals
                price = Math.Round(price, 2);

                // 4. Quantity (0.01 to 2.0 BTC)
                var quantity = Math.Round((decimal)(_random.NextDouble() * 2.0 + 0.01), 4);

                // 5. Place Order
                var orderRequest = new PlaceOrderRequest(
                    Symbol: symbol,
                    Side: side,
                    Type: OrderType.Limit, // Always Limit for orderbook visibility
                    Price: price,
                    Quantity: quantity
                );

                await grain.PlaceOrderAsync(orderRequest);

                // 6. Metrics & Logging
                _orderCounter.Add(1);
                
                // Only log every 100th order to avoid spamming console
                if (_random.Next(100) == 0) 
                {
                    _logger.LogInformation("[User {UserId}] Placed {Side} @ {Price} (x{Qty})", userId, side, price, quantity);
                }

                await Task.Delay(intervalMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[User {UserId}] Error placing order", userId);
                await Task.Delay(1000, ct); // Backoff on error
            }
        }
    }

    private void UpdatePrice()
    {
        // Random Walk: +/- 0.5% max change
        var change = (decimal)(_random.NextDouble() - 0.5) * 0.01m; 
        _currentPrice += _currentPrice * change;
        
        // Keep price sanity
        if (_currentPrice < 10000) _currentPrice = 10000;
        if (_currentPrice > 100000) _currentPrice = 100000;
    }
}
