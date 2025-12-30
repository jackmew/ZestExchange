# 買賣盤的核心排序邏輯。在撮合引擎中，誰排在第一位是最重要的事情

###  1. 費曼學習法解釋：為什麼一個要反轉，一個不用？

  想像你在拍賣會場：

   * _asks (賣家佇列) = 「比便宜」
       * 你要買東西，你會先看最便宜的賣家。
       * 所以賣單要從低到高排序（Ascending）。
       * 預設行為：SortedDictionary 預設就是從小排到大 (100, 101, 102...)，所以 _asks 不需要任何參數。

   * _bids (買家佇列) = 「比有錢」
       * 你要賣東西，你會先賣給出價最高的買家。
       * 所以買單要從高到低排序（Descending）。
       * 反轉行為：我們必須告訴電腦「把大的排前面」。
       * 程式碼 (a, b) => b.CompareTo(a) 就是這個意思。

  技術細節：Comparer 的魔法
   * 正常 (a vs b): a.CompareTo(b) → 若 a < b 回傳 -1 (a 排前面)。結果：1, 2, 3。
   * 反轉 (b vs a): b.CompareTo(a) → 若 a < b，b 就大，回傳 1 (b 排前面)。結果：3, 2, 1。

##   2. Order 資料結構

這是你在 ZestExchange.Silo/Domain/OrderBook/Order.cs 定義的實體結構。它是「Rich Domain Model」，包含資料與計算屬性。

```c#
    1 public class Order
    2 {
    3     // 唯一識別碼 (資料庫/系統追蹤用)
    4     public Guid Id { get; }
    5
    6     // 交易對 (例如 "BTC-USDT")
    7     public string Symbol { get; }
    8
    9     // 方向 (Buy=買入, Sell=賣出)
   10     public OrderSide Side { get; }
   11
   12     // 類型 (Limit=限價單, Market=市價單)
   13     public OrderType Type { get; }
   14
   15     // 價格 (你出多少錢)
   16     public decimal Price { get; }
   17
   18     // 原始下單數量 (你要買幾顆)
   19     public decimal Quantity { get; private set; }
   20
   21     // 已成交數量 (已經買到幾顆) - 這是變動的狀態
   22     public decimal FilledQuantity { get; private set; }
   23
   24     // 計算屬性：還剩多少沒買到 (Quantity - FilledQuantity)
   25     // 撮合引擎主要看這個數字
   26     public decimal RemainingQuantity => Quantity - FilledQuantity;
   27
   28     // 狀態 (New, PartiallyFilled, Filled, Cancelled)
   29     public OrderStatus Status { get; private set; }
   30
   31     // 建立時間 (排隊順序依據)
   32     public DateTime CreatedAt { get; }
   33 }
```

## 3. Mock Data (模擬情境資料)

這裡模擬了四種不同狀態的訂單，讓你更有感覺：

  情境 A：剛下單的新單 (New)
   * 小明想用 $50,000 買 1 顆比特幣，還沒人理他。

   1 var newOrder = new Order(
   2     id: Guid.NewGuid(),
   3     symbol: "BTC-USDT",
   4     side: OrderSide.Buy,
   5     type: OrderType.Limit,
   6     price: 50000m,
   7     quantity: 1.0m
   8 );
   9 // 結果: Status = New, RemainingQuantity = 1.0

  情境 B：部分成交的單 (Partially Filled)
   * 小華想賣 2 顆比特幣，剛剛有人買走了 0.5 顆。
```c#
    1 var partialOrder = new Order(
    2     id: Guid.NewGuid(),
    3     symbol: "BTC-USDT",
    4     side: OrderSide.Sell,
    5     type: OrderType.Limit,
    6     price: 55000m,
    7     quantity: 2.0m
    8 );
    9 // 模擬撮合引擎呼叫 Fill
   10 partialOrder.Fill(0.5m);
   11
   12 // 結果: Status = PartiallyFilled, FilledQuantity = 0.5, RemainingQuantity = 1.5
```C3

  情境 C：完全成交的單 (Filled)
   * 小美想買 0.1 顆比特幣，立刻成交了。
```c#
    1 var filledOrder = new Order(
    2     id: Guid.NewGuid(),
    3     symbol: "BTC-USDT",
    4     side: OrderSide.Buy,
    5     type: OrderType.Limit,
    6     price: 50000m,
    7     quantity: 0.1m
    8 );
    9 filledOrder.Fill(0.1m);
   10
   11 // 結果: Status = Filled, RemainingQuantity = 0.0
```
  情境 D：被取消的單 (Cancelled)
   * 老王掛單後覺得價格不好，不想賣了。
```c#
    1 var cancelledOrder = new Order(
    2     id: Guid.NewGuid(),
    3     symbol: "BTC-USDT",
    4     side: OrderSide.Sell,
    5     type: OrderType.Limit,
    6     price: 60000m,
    7     quantity: 5.0m
    8 );
    9 cancelledOrder.Cancel();
   10
   11 // 結果: Status = Cancelled
```


# 我們用「市集擺攤」的比喻，帶你走一遍小明的訂單旅程：

## 小明想用 $50,000 買 1 顆比特幣，還沒人理他。 那這筆newOrder 進入到哪個_asks or _bids ?

我們用「市集擺攤」的比喻，帶你走一遍小明的訂單旅程：

## 1. 答案：進入 _bids (買盤)

小明是買家，他出的價錢叫做 Bid Price (買標價)。
* _bids: 存放所有「我想買，但還沒買到」的人（買盤）。
* _asks: 存放所有「我想賣，但還沒賣掉」的人（賣盤）。


## 2. 小明訂單的生命週期 (Step-by-Step)

當小明按下「下單」按鈕，後端執行 OrderBookEngine.PlaceOrder(...)：

第一步：建立訂單實體
系統幫小明建立一個 Order 物件。
* Side: Buy
* Price: 50,000
* Quantity: 1.0
* RemainingQuantity: 1.0

第二步：嘗試撮合 (Match)
引擎不會立刻把單掛在牆上，它會先去對面（_asks 賣盤）看有沒有便宜貨。
* 引擎檢查 _asks 的第一名（最便宜的賣家）。
* 情境 1：如果最便宜的賣家要賣 $49,000，小明願意付 $50,000，成交！ (Trade)
* 情境 2 (你的案例)：現在最便宜的賣家要賣 $51,000，小明只願付 $50,000。
    * 結果：價格對不上，無法成交。小明的 RemainingQuantity 還是 1.0。

第三步：進入買盤 (AddToBook)
因為沒買到，這張單不能消失，必須「掛在牆上」等別人來賣給他。
* 引擎執行 AddToBook(小明的單)。
* 程式碼邏輯：var book = order.Side == OrderSide.Buy ? _bids : _asks;
* 因為是 Buy，所以選擇進入 `_bids`。

## 3. 在記憶體裡的細部結構 (Visualized)

  這時候 OrderBookEngine 內部的 _bids (SortedDictionary) 會長這樣：
```
   1 _bids (SortedDictionary - 價格從高到低)
   2 │
   3 ├── Key: 50000 (decimal)  <-- 小明的價格層級
   4 │   └── Value: LinkedList<Order>
   5 │       └── [小明的 Order 物件] (Node)
   6 │
   7 ├── Key: 49900 (decimal)
   8 │   └── Value: LinkedList<Order>
   9 │       └── [老王的單] -> [老李的單] (FIFO 隊伍)
```
  同時，為了 O(1) 快速取消，`_orderLookup` 也會多一筆資料：
   * Key: 小明的 OrderId (Guid)
   * Value: OrderLocation { 指向 $50,000 這一層, 指向小明在隊列裡的節點 }

## 4. 為什麼叫 "Asks" 和 "Bids"？(第一性原理)

這兩個英文單字在金融業有特定意義，記住這個口訣就不會搞混：

* Bid (出價)：買家說：「我出(Bid) 50,000 買這個！」
    * 所以 _bids 是買盤。
* Ask (要求)：賣家說：「我要求(Ask) 51,000 才賣這個！」
    * 所以 _asks 是賣盤。