# 加 FastEndpoints + Swagger
```
cd /Users/zest/Projects/c-net/ZestExchange/ZestExchange.ApiService 
dotnet add package FastEndpoints 
dotnet add package FastEndpoints.Swagger
```

## EndPoints

```

  Orders API

  | Method | Path             | 用途             |
  |--------|------------------|------------------|
  | POST   | /api/orders      | 下單             |
  | GET    | /api/orders      | 查詢我的所有訂單 |
  | GET    | /api/orders/{id} | 查詢單一訂單詳情 |
  | DELETE | /api/orders/{id} | 取消訂單         |

  Query 參數 (GET /api/orders):
  ?symbol=BTC-USDT    # 篩選交易對
  &status=New         # 篩選狀態
  &limit=50           # 回傳數量上限

  OrderBook API

  | Method | Path                    | 用途               |
  |--------|-------------------------|--------------------|
  | GET    | /api/orderbook/{symbol} | 取得交易對的買賣盤 |

  ---
  備註: 目前是 mock，真實系統會需要 User ID (從 JWT token 取得) 來過濾訂單。繼續 Step 5？

  範例

  # 下單
  POST /api/orders
  { "symbol": "BTC-USDT", "side": "Buy", "type": "Limit", "price": 50000, "quantity": 1.5 }

  # 取消
  DELETE /api/orders/550e8400-e29b-41d4-a716-446655440000

  # 查詢訂單
  GET /api/orders/550e8400-e29b-41d4-a716-446655440000

  # 取得盤口 (top 10 買賣)
  GET /api/orderbook/BTC-USDT?depth=10

  Response 範例 (OrderBook):
  {
    "symbol": "BTC-USDT",
    "bids": [
      { "price": 49900, "totalQuantity": 5.5 },
      { "price": 49800, "totalQuantity": 3.2 }
    ],
    "asks": [
      { "price": 50100, "totalQuantity": 2.1 },
      { "price": 50200, "totalQuantity": 4.8 }
    ]
  }



  FastEndpoints 官方推薦一個 endpoint 一個 class，但如果你偏好分組，可以用資料夾：

  Endpoints/
  ├── Orders/
  │   ├── PlaceOrderEndpoint.cs
  │   ├── CancelOrderEndpoint.cs
  │   └── GetOrderEndpoint.cs
  └── OrderBook/
      └── GetOrderBookEndpoint.cs
```


# Contracts

Contract = 合約/契約，表示服務之間的「約定」。
```
  ┌─────────────────┐         ┌─────────────────┐
  │   ApiService    │ ←─────→ │   Orleans Silo  │
  │   (Client)      │   共用   │   (Server)      │
  └─────────────────┘  Contract └─────────────────┘
```
  這個 Contracts 專案之後會放：
  - DTOs - Request/Response 資料結構
  - Enums - 共用列舉
  - Grain Interfaces - Orleans 的 IMatchingEngineGrain (Step 7)

  命名慣例比較

  | Java Spring Boot         | C# .NET                    | 說明          |
  |--------------------------|----------------------------|---------------|
  | dto/ package             | Contracts/ 或 Shared/      | DTOs          |
  | model/                   | Domain/ 或 Entities/       | Domain models |
  | Interface in same module | Separate Contracts project | Orleans 慣例  |

  其他常見命名：Shared、Common、Models，但 Orleans 生態系習慣用 Contracts。

  Import 語法

  // Java
  import com.zest.exchange.dto.PlaceOrderRequest;

  // C#
  using ZestExchange.Contracts.Orders;

  是的，using ≈ Java 的 import。

  專案引用則在 .csproj：
  <ProjectReference Include="..\ZestExchange.Contracts\ZestExchange.Contracts.csproj" />

  這樣 ApiService 和之後的 Silo 都能共用同一份類型定義。
