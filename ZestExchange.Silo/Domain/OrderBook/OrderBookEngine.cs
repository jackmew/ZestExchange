using ZestExchange.Contracts.Orders;
using ZestExchange.Contracts.OrderBook;

namespace ZestExchange.Silo.Domain.OrderBook;

/// <summary>
/// Hybrid OrderBook Engine - 業界高手做法
///
/// Data Structures:
/// - SortedDictionary for price levels (O(log M) insert/lookup, M = price levels)
/// - LinkedList<Order> at each price level (FIFO ordering)
/// - Dictionary<Guid, OrderLocation> for O(1) order lookup and cancellation
///
/// Complexity:
/// - Place Order: O(log M)
/// - Cancel Order: O(1)  ← Key advantage over PriorityQueue
/// - Match: O(1) per trade
/// </summary>
public class OrderBookEngine
{
    private readonly string _symbol;

    // Bids: highest price first (descending) - (買家佇列) = 「比有錢」
    private readonly SortedDictionary<decimal, LinkedList<Order>> _bids
        = new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

    // Asks: lowest price first (ascending)
    private readonly SortedDictionary<decimal, LinkedList<Order>> _asks = new();

    // O(1) lookup for cancel operation
    private readonly Dictionary<Guid, OrderLocation> _orderLookup = new(); //Guid: orderId

    public OrderBookEngine(string symbol)
    {
        _symbol = symbol;
    }

    /// <summary>
    /// Place a new order and attempt to match
    /// </summary>
    public (Order order, List<Trade> trades) PlaceOrder(
        OrderSide side,
        OrderType type,
        decimal price,
        decimal quantity)
    {
        // 1. Taker 剛出生
        var order = new Order(
            id: Guid.NewGuid(),
            symbol: _symbol,
            side: side,
            type: type,
            price: price,
            quantity: quantity);

        var trades = new List<Trade>();

        // Try to match
        trades = Match(order); // 2. Taker 嘗試去「吃」別人 (Match

        // If order still has remaining quantity, add to book - 只有當 Taker 吃飽了還有剩，或者根本吃不到時, 才會把他「掛」到牆上 (AddToBook)
        if (order.RemainingQuantity > 0 && type == OrderType.Limit)
        {
            AddToBook(order);
        }

        return (order, trades);
    }

    /// <summary>
    /// Cancel an order - O(1) complexity
    /// </summary>
    /*
      引用型別 (Reference Type)

      這時候，記憶體裡並沒有產生兩個隊伍。實際上發生的事是：
        * 記憶體位址 0x123 存著這條隊伍。
        * _bids[50000] 指向 0x123。
        * _orderLookup[ID].PriceLevel 也指向 0x123。

        _orderLookup[ID].Node 也是引用
    */
    public bool CancelOrder(Guid orderId)
    {   
        if (!_orderLookup.TryGetValue(orderId, out var location))
        {
            return false;
        }

        var order = location.Node.Value;
        order.Cancel();

        // Remove from linked list - O(1)
        location.PriceLevel.Remove(location.Node);

        // Remove empty price level
        if (location.PriceLevel.Count == 0)
        {
            var book = order.Side == OrderSide.Buy ? _bids : _asks;
            book.Remove(order.Price);
        }

        // Remove from lookup
        _orderLookup.Remove(orderId);

        return true;
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    public Order? GetOrder(Guid orderId)
    {
        return _orderLookup.TryGetValue(orderId, out var location)
            ? location.Node.Value
            : null;
    }

    /// <summary>
    /// Get orderbook snapshot
    /// </summary>
    /// 
    /// 
    /*
    當你呼叫 GetSnapshot(depth: 2) 時，回傳的資料看起來會像這樣：
    1 {
    2   "symbol": "BTC-USDT",
    3   "bids": [
    4     { "price": 50000.0, "totalQuantity": 5.5 }, // 這是小明(1.0) + 老王(4.5) 的加總
    5     { "price": 49990.0, "totalQuantity": 10.2 }
    6   ],
    7   "asks": [
    8     { "price": 50100.0, "totalQuantity": 2.1 },
    9     { "price": 50200.0, "totalQuantity": 4.8 }
   10   ],
   11   "timestamp": "2023-10-27T10:00:00Z"
   12 }
    */
    public GetOrderBookResponse GetSnapshot(int depth = 10)
    {
        var bids = _bids
            .Take(depth)
            .Select(kv => new PriceLevelDto(kv.Key, kv.Value.Sum(o => o.RemainingQuantity)))
            .ToList();

        var asks = _asks
            .Take(depth)
            .Select(kv => new PriceLevelDto(kv.Key, kv.Value.Sum(o => o.RemainingQuantity)))
            .ToList();

        return new GetOrderBookResponse(_symbol, bids, asks, DateTime.UtcNow);
    }

    /// <summary>
    /// Match incoming order against opposite side
    /// </summary>
    private List<Trade> Match(Order takerOrder)
    {
        var trades = new List<Trade>();
        var oppositeBook = takerOrder.Side == OrderSide.Buy ? _asks : _bids; //對手

        // 核心迴圈：只要我還沒買夠/賣完 (RemainingQuantity > 0)
        // 且對手盤還有單 (oppositeBook.Count > 0)，就繼續撮
        while (takerOrder.RemainingQuantity > 0 && oppositeBook.Count > 0)
        {
            var bestPrice = oppositeBook.First().Key;       // 對手盤最好的價格
            var priceLevel = oppositeBook.First().Value;    // 那個價格的排隊隊伍

            // Check if price matches
            // 是否價格有對上 才有可能成交(下一步就是看剩餘數量RemainingQuantity)
            bool priceMatches = takerOrder.Side == OrderSide.Buy
                ? takerOrder.Price >= bestPrice  // Buy: willing to pay >= ask price.     買單：我出 50000 >= 對手賣 49000
                : takerOrder.Price <= bestPrice; // Sell: willing to accept <= bid price. 賣單：我賣 49000 <= 對手買 50000

            // Market order always matches
            // 「如果這是一張『限價單 (Limit Order)』，而且價格『談不攏(!priceMatches)』，那就別玩了，直接回家 (Break)。」
            if (takerOrder.Type != OrderType.Market && !priceMatches)
            {
                break;
            }

            // Match against orders at this price level (FIFO)
            while (takerOrder.RemainingQuantity > 0 && priceLevel.Count > 0)
            {
                var makerOrder = priceLevel.First!.Value;
                // 拆單 (Partial Fill)
                /*
                  這行程式碼完美體現了拆單邏輯：
                    * 情境：小明想買 10 顆 (taker)，對手老王只賣 3 顆 (maker)。
                    * 結果：Min(10, 3) = 3。
                    * 動作：
                        * 小明買到了 3 顆（還剩 7 顆）。
                        * 老王賣掉了 3 顆（剩 0 顆，賣完了）。
                        * 這就是一筆成交記錄 (Trade)，數量是 3。

                    接著迴圈會繼續跑 (while)，因為小明還有 7 顆沒買到 (takerOrder.RemainingQuantity >
                    0)，他會繼續吃下一個賣家的單。
                */
                var matchQty = Math.Min(takerOrder.RemainingQuantity, makerOrder.RemainingQuantity);

                // Execute trade
                takerOrder.Fill(matchQty);
                makerOrder.Fill(matchQty);

                var trade = new Trade(
                    TradeId: Guid.NewGuid(),
                    MakerOrderId: makerOrder.Id,
                    TakerOrderId: takerOrder.Id,
                    Price: makerOrder.Price,  // Trade at maker's price
                    Quantity: matchQty,
                    Timestamp: DateTime.UtcNow);

                trades.Add(trade);

                // Remove filled maker order
                if (makerOrder.RemainingQuantity == 0)
                {
                    _orderLookup.Remove(makerOrder.Id);
                    priceLevel.RemoveFirst();
                }
                /*
                  Q:為什麼 TakerOrder 不需要被移除？   
                  A:Taker 這時候根本還沒「進入」OrderBook。

                    關鍵點：
                    * Maker (對手盤)：他們是早就掛在牆上（OrderBook 裡）的人。所以當他們被吃光
                        (RemainingQuantity == 0)，我們必須把他們從牆上撕下來 (priceLevel.RemoveFirst() +
                        _orderLookup.Remove)。
                    * Taker (你這張單)：他在 Match
                        函數執行時，還只是一個在記憶體裡遊蕩的自由靈魂，還沒被掛上去。
                        * 如果他在 Match
                            裡被完全滿足了（買滿了），那他直接就結束了，回傳給用戶就好。既然從來沒掛上去過，
                            自然就不需要移除。
                        * 只有當他 Match 完還剩下一些，才會在 PlaceOrder 的最後一步被 AddToBook 掛上去。

                    費曼總結

                    * Maker (牆上的單)：像是超市架上的商品。被買走就要下架。
                    * Taker (你的單)：像是你的購物籃。
                        * 你拿著籃子去裝商品（Match）。
                        * 如果籃子裝滿了，你就結帳走人（不需要下架，因為你根本沒上架過）。
                        * 如果籃子沒裝滿，超市又沒貨了，你才會決定把剩下的需求寫在紙條上貼在牆上（這時候你
                            才變成了新的 Maker）。

                    所以程式碼邏輯是完全正確的：Maker 需要被移除，Taker 不需要（因為他還不在上面）。
                */
                

            }

            // Remove empty price level
            if (priceLevel.Count == 0)
            {
                oppositeBook.Remove(bestPrice);
            }
        }

        return trades;
    }

    /// <summary>
    /// 掛單
    /// Add order to the book - O(log M)
    /// </summary>
    private void AddToBook(Order order)
    {
        // book: LinkedList<Order>
        var book = order.Side == OrderSide.Buy ? _bids : _asks;

        /*
            TryGetValue (嘗試獲取值)
            * 為什麼不用 `book[order.Price]` 直接拿？
                * 如果櫃子裡根本沒有這個價格，直接拿會導致程式崩潰 (Exception)。
                * TryGetValue 是安全做法：它回傳 true（找到了）或 false（沒找到），不會崩潰。

            out var priceLevel (輸出變數)
            這是 C# 的特殊語法。通常一個函數只能回傳一個東西，但 out 讓它能多帶一個東西出來。
            * 回傳值 (bool)：代表「有沒有找到」。
            * out 變數 (priceLevel)：
                * 如果找到了：priceLevel 就會裝著那個已經存在的 LinkedList。
                * 如果沒找到：priceLevel 會是空的 (null)。
        
            常見寫法：
            var pl = book.GetValueOrDefault(price); // 第 1 次搜尋
            if (pl == null) { ... }
            雖然在你的案例中 priceLevel 是 LinkedList（引用型別），可以用 null 檢查，但 .NET 工程師通常習慣統一用 out 寫法，這被視為一種 「專業的肌肉記憶」。

            為什麼 .NET 這麼愛 out？
                因為 C# 想要在一個動作內完成兩件事：
                1. 告訴你「有沒有」(bool)。
                2. 順便把「東西」給你(out)。

                如果拆成兩次動作，在像交易所這種每秒幾十萬筆交易的系統中，累積起來的 CPU 耗損會非常驚人。
        */

        if (!book.TryGetValue(order.Price, out var priceLevel))
        {
            priceLevel = new LinkedList<Order>();
            book[order.Price] = priceLevel;
        }

    /*
      在撮合引擎中，Price-Time Priority (價格-時間優先) 是絕對真理：
        1. Price (價格優先)：買家出價越高越優先，賣家要價越低越優先。
        2. Time (時間優先)：如果價格一樣，先來的人先成交。
    */
    // LinkedList.AddLast 確保了新來的單永遠排在舊單的後面。隊伍長這樣：
    // 
        var node = priceLevel.AddLast(order);


        _orderLookup[order.Id] = new OrderLocation(priceLevel, node);
    }

    /// <summary>
    /// Stores the location of an order for O(1) access
    /// </summary>
    private readonly record struct OrderLocation(
        // 它在「哪一個價格」的隊伍裡？
        // 存 `PriceLevel`：因為取消訂單後，如果那個價格沒人了，我們要負責把整個價格層級從SortedDictionary 移除。
        LinkedList<Order> PriceLevel,
        
        // 它在隊伍裡的「具體記憶體位置」在哪？
        // 存 `Node`：這是最天才的地方。LinkedList 的 Node就像是隊伍中那個人的衣角。只要抓到衣角，我們可以直接把他「從隊伍中抽出來」，完全不需要重新搜尋。
        LinkedListNode<Order> Node);  
}
