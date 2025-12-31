using Orleans;
using Orleans.Streams;
using ZestExchange.Contracts.Grains;
using ZestExchange.Contracts.Orders;
using ZestExchange.Contracts.OrderBook;
using ZestExchange.Contracts.Events;
using ZestExchange.Silo.Domain.OrderBook;

namespace ZestExchange.Silo.Grains;

/// <summary>
/// Orleans Grain 實作 - 撮合引擎
///
/// 每個交易對 (Symbol) 一個 Grain 實例
/// Orleans 保證同一時間只有一個執行緒執行 = Lock-free
/// </summary>
public class MatchingEngineGrain : Grain, IMatchingEngineGrain
{
    private readonly ILogger<MatchingEngineGrain> _logger;
    private OrderBookEngine _orderBook = null!;
    private string _symbol = null!;

    // Orleans Stream for real-time OrderBook updates
    private IAsyncStream<OrderBookUpdated>? _orderBookStream;
    // Orleans Stream for real-time Trade updates
    private IAsyncStream<TradeOccurred>? _tradeStream;

    /*
       * 問題：MatchingEngineGrain 是一個 單例 (Singleton-like) 的物件（它會活很久，直到被
        Deactivate）。但是我們的 TradeRepository 是 Scoped
        的（通常跟著一個請求活，或者短暫存活）。如果直接注入 Scoped 到 Singleton，會導致
        Memory Leak 或是 DbContext 被多個執行緒同時使用而炸掉。
    * 解法：IServiceScopeFactory 是一個「產生器」。它讓 Grain
        可以隨時說：「我現在需要用一下資料庫，請給我一個短暫的 Scope。」
    */
    private readonly IServiceScopeFactory _scopeFactory;

    public MatchingEngineGrain(
        ILogger<MatchingEngineGrain> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Grain Key = Symbol (e.g., "BTC-USDT")
        _symbol = this.GetPrimaryKeyString();
        _orderBook = new OrderBookEngine(_symbol);

        // Initialize Orleans Stream
        var streamProvider = this.GetStreamProvider("OrderBookProvider");
        _orderBookStream = streamProvider.GetStream<OrderBookUpdated>(
            StreamId.Create("orderbook", _symbol));
        _tradeStream = streamProvider.GetStream<TradeOccurred>(
            StreamId.Create("trades", _symbol));

        // Ensure DB Table Exists (Simple Code First)
        // In production, use EF Migrations or specialized migration tool
        await Task.Run(() => 
        {
            using var scope = _scopeFactory.CreateScope(); // 1. 建立一個全新的小房間
            var repo = scope.ServiceProvider.GetRequiredService<ZestExchange.Repository.TradeRepository>(); // 2.在房間裡拿出 Repository
            repo.EnsureDatabaseCreated(); // 3. 用完後，using 結束，房間銷毀，資料庫連線釋放
        });

        _logger.LogInformation("MatchingEngineGrain activated for {Symbol}", _symbol);

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        _logger.LogInformation(
            "PlaceOrder: {Symbol} {Side} {Type} Price={Price} Qty={Quantity}",
            _symbol, request.Side, request.Type, request.Price, request.Quantity);

        var (order, trades) = _orderBook.PlaceOrder(
            request.Side,
            request.Type,
            request.Price,
            request.Quantity);

        if (trades.Count > 0)
        {
            _logger.LogInformation(
                "Order {OrderId} matched {TradeCount} trades",
                order.Id, trades.Count);

            // 1. Publish to Stream (Real-time)
            if (_tradeStream != null)
            {
                var tasks = new List<Task>(trades.Count);
                foreach (var trade in trades)
                {
                    var tradeEvent = new TradeOccurred(
                        Symbol: _symbol,
                        Price: trade.Price,
                        Quantity: trade.Quantity,
                        TakerSide: request.Side, 
                        Timestamp: trade.Timestamp);
                    
                    tasks.Add(_tradeStream.OnNextAsync(tradeEvent));
                }
                await Task.WhenAll(tasks);
            }

            // 2. Persist to DB (Fire-and-forget / Async)
            // We don't want to block the matching engine for DB IO

            /*
               * 背景：PlaceOrderAsync 的主要任務是撮合訂單，這件事必須極快 (微秒級)。寫入資料庫
                (IO) 相對來說極慢 (毫秒級)。
            * Fire-and-Forget (射後不理)：
                * Task.Run：把寫入 DB 的工作丟到 ThreadPool 裡的另一個執行緒去跑。
                * _ = (Discard)：告訴編譯器「我知道這個 Task
                    有回傳值，但我不在乎，也不想等待它」。
            * 效果：當這行代碼執行時，主執行緒會立刻往下走，回傳給用戶「下單成功」。用戶不需要等
                待資料庫寫入完成。這大幅降低了延遲 (Latency)。
            * 風險：如果資料庫寫入失敗，用戶不會知道（只會記在 Log
                裡）。但在高頻交易中，這是為了效能所做的權衡。
            
            */
            _ = Task.Run(async () => 
            {
                try 
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ZestExchange.Repository.TradeRepository>();
                    
                    var entities = trades.Select(t => new ZestExchange.Repository.Entities.TradeHistory
                    {
                        Id = Guid.NewGuid(),
                        Symbol = _symbol,
                        Price = t.Price,
                        Quantity = t.Quantity,
                        TakerSide = request.Side,
                        MakerOrderId = t.MakerOrderId,
                        TakerOrderId = t.TakerOrderId,
                        ExecutedAt = t.Timestamp
                    }).ToList();
                    // 這是我們在 TradeRepository.cs 裡封裝的方法，底層呼叫了 SqlSugar 的 BulkCopy。
                    await repo.BulkInsertAsync(entities);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist trades to DB");
                }
            });
        }

        // Publish OrderBook update to stream
        await PublishOrderBookUpdateAsync();

        return new PlaceOrderResponse(
            OrderId: order.Id,
            Status: order.Status,
            Message: trades.Count > 0
                ? $"Matched {trades.Count} trade(s)"
                : "Order placed in book");
    }

    public async Task<CancelOrderResponse> CancelOrderAsync(Guid orderId)
    {
        _logger.LogInformation("CancelOrder: {OrderId}", orderId);

        var success = _orderBook.CancelOrder(orderId);

        if (success)
        {
            // Publish OrderBook update to stream
            await PublishOrderBookUpdateAsync();
        }

        return new CancelOrderResponse(
            OrderId: orderId,
            Success: success,
            Message: success ? "Order cancelled" : "Order not found");
    }

    public Task<GetOrderResponse?> GetOrderAsync(Guid orderId)
    {
        var order = _orderBook.GetOrder(orderId);

        if (order == null)
        {
            return Task.FromResult<GetOrderResponse?>(null);
        }

        return Task.FromResult<GetOrderResponse?>(new GetOrderResponse(
            OrderId: order.Id,
            Symbol: order.Symbol,
            Side: order.Side,
            Type: order.Type,
            Price: order.Price,
            Quantity: order.Quantity,
            FilledQuantity: order.FilledQuantity,
            Status: order.Status,
            CreatedAt: order.CreatedAt));
    }

    public Task<GetOrderBookResponse> GetOrderBookAsync(int depth = 10)
    {
        return Task.FromResult(_orderBook.GetSnapshot(depth));
    }

    /// <summary>
    /// Publish current OrderBook state to Orleans Stream
    /// Subscribers (Blazor components) receive real-time updates
    /// </summary>
    private async Task PublishOrderBookUpdateAsync()
    {
        if (_orderBookStream == null) return;

        var snapshot = _orderBook.GetSnapshot(5);
        var update = new OrderBookUpdated(
            Symbol: _symbol,
            Bids: snapshot.Bids,
            Asks: snapshot.Asks,
            Timestamp: DateTime.UtcNow);

        await _orderBookStream.OnNextAsync(update);

        _logger.LogDebug("Published OrderBook update for {Symbol}", _symbol);
    }
}
