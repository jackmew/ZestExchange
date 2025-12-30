# global.json
強制此專案使用 .NET 9.0.200+。

# Aspire Dashboard
https://localhost:17256/login?t=393f271867d377c821ea158ba936d855



#  為什麼需要 Contracts？
```
  ┌─────────────────┐         ┌─────────────────┐
  │   API Service   │  ───►   │   Orleans Silo  │
  │  (呼叫 Grain)   │         │  (實作 Grain)   │
  └─────────────────┘         └─────────────────┘
           │                          │
           └──────────┬───────────────┘
                      ▼
             ┌─────────────────┐
             │    Contracts    │
             │  (共用的介面)    │
             │  - IMatchingEngineGrain (介面)
             │  - PlaceOrderRequest (DTO)
             │  - OrderBookSnapshot (DTO)
             └─────────────────┘

  簡單說： API 和 Silo 都需要知道「怎麼溝通」，所以把介面和 DTO 放在共用專案。
  ```

# 核心撮合引擎

```
ZestExchange.Silo/
├── Domain/
│   └── OrderBook/
│       ├── Order.cs            # 訂單實體
│       ├── Trade.cs            # 成交記錄
│       └── OrderBookEngine.cs  # 撮合引擎 (核心)

```

##  OrderBookEngine 資料結構

```
  ┌─────────────────────────────────────────────────────────┐
  │                    OrderBookEngine                       │
  ├─────────────────────────────────────────────────────────┤
  │  _bids: SortedDictionary<price, LinkedList<Order>>      │
  │         (降序 - 最高價優先)                               │
  │                                                          │
  │  _asks: SortedDictionary<price, LinkedList<Order>>      │
  │         (升序 - 最低價優先)                               │
  │                                                          │
  │  _orderLookup: Dictionary<Guid, OrderLocation>          │
  │                (O(1) 取消用)                             │
  └─────────────────────────────────────────────────────────┘
```

## 複雜度 (面試重點)

| 操作              | 複雜度   | 說明                    |
|-------------------|----------|-------------------------|
| Place Order       | O(log M) | M = 價格層級數          |
| Cancel Order      | O(1)     | 直接找到 LinkedListNode |
| Match (per trade) | O(1)     | 從 sorted head 取出     |
| Get Snapshot      | O(depth) | 取前 N 層               |

為什麼比 PriorityQueue 好？

PriorityQueue:  Cancel = O(N) - 要搜尋整個 heap
Hybrid:         Cancel = O(1) - Dictionary 直接定位


## Domain-Driven Design (DDD) - Order is Rich Domain Model
Domain 是什麼？

Domain = 業務邏輯核心 (交易所的核心業務)

```
ZestExchange.Silo/
├── Domain/              ← 業務邏輯層
│   └── OrderBook/
│       ├── Order.cs     ← Domain Entity (有行為)
│       └── OrderBookEngine.cs
├── Grains/              ← Orleans 接入層 (Step 7)
└── Program.cs 
```


Order.cs - Entity vs DTO

  | 類型   | 位置       | 特性                 |
  |--------|------------|----------------------|
  | DTO    | Contracts/ | 純資料，沒有行為     |
  | Entity | Domain/    | 有資料 + 行為 (方法) |

```c#
  // DTO (Contracts) - 只有資料
  public record PlaceOrderRequest(string Symbol, decimal Price, decimal Quantity);

  // Entity (Domain) - 有資料 + 行為
  public class Order
  {
      public decimal FilledQuantity { get; private set; }
      public OrderStatus Status { get; private set; }

      // 行為：成交
      public void Fill(decimal quantity)
      {
          FilledQuantity += quantity;
          Status = FilledQuantity >= Quantity
              ? OrderStatus.Filled
              : OrderStatus.PartiallyFilled;
      }

      // 行為：取消
      public void Cancel()
      {
          Status = OrderStatus.Cancelled;
      }
  }

```

  為什麼這樣設計？

  封裝業務規則：

  // ❌ 不好 - 業務邏輯散落在外面
  order.FilledQuantity += 100;
  if (order.FilledQuantity >= order.Quantity)
      order.Status = OrderStatus.Filled;

  // ✅ 好 - 業務邏輯封裝在 Entity 內
  order.Fill(100);  // 自動更新 Status

  DDD 層級對照

  | 層級           | 專案         | 職責                                  |
  |----------------|--------------|---------------------------------------|
  | Domain         | Silo/Domain/ | 核心業務邏輯 (Order, OrderBookEngine) |
  | Application    | Silo/Grains/ | 用例協調 (Orleans Grain)              |
  | Infrastructure | Silo/        | 技術細節 (Redis, DB)                  |
  | Contracts      | Contracts/   | API 介面定義                          |

  面試可以提到：「Order 是 Rich Domain Model，不只是資料袋，還封裝了業務行為。」