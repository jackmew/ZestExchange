
## Orleans Streams - SignalR

    SignalR 是 ASP.NET Core 內建的，不需要 Azure。
```
  | 環境       | SignalR                                  |
  |------------|------------------------------------------|
  | Local Dev  | ✅ 內建，直接用                          |
  | Production | Azure SignalR Service (可選，用於 scale) |

  ---
  ```
  Orleans Streams 架構

  你提到的 samples 很好！讓我解釋：
```
  ┌─────────────────────────────────────────────────────────────┐
  │                      Orleans Silo                            │
  │  ┌──────────────────┐      ┌──────────────────┐             │
  │  │MatchingEngine    │      │ PushNotifier     │             │
  │  │Grain (BTC-USDT)  │─────▶│ Grain            │             │
  │  │                  │ emit │ (SignalR Hub)    │             │
  │  │ OrderBookEngine  │ event│                  │             │
  │  └──────────────────┘      └────────┬─────────┘             │
  │                                     │                        │
  │         Orleans Stream              │ SignalR                │
  │         (Memory/Redis)              │ WebSocket              │
  └─────────────────────────────────────┼───────────────────────┘
                                        │
                                        ▼
                                ┌───────────────┐
                                │ Web (Blazor)  │
                                │ Real-time     │
                                │ OrderBook     │
                                └───────────────┘
```

#  Orleans Streams vs Browser 連接

  Orleans Streams 不能直接連接 Browser - 這是對的！

  Orleans Streams 是 Server-side only 的 Pub/Sub 機制，運行在 Orleans Cluster 內部。

  ChatRoom Sample 的運作方式
```
  ┌─────────────────────────────────────────────────────────────┐
  │                    Server Side                               │
  │  ┌────────────┐    Orleans     ┌──────────────────┐         │
  │  │  Orleans   │    Stream      │  Blazor Server   │         │
  │  │   Grain    │ ───────────────▶  Component       │         │
  │  └────────────┘                │  (subscribes)    │         │
  │                                └────────┬─────────┘         │
  │                                         │                    │
  └─────────────────────────────────────────┼────────────────────┘
                                            │ SignalR (built-in)
                                            ▼
  ┌─────────────────────────────────────────────────────────────┐
  │                    Browser                                   │
  │  ┌──────────────────────────────────────┐                   │
  │  │  Blazor WASM/JS (receives UI update) │                   │
  │  └──────────────────────────────────────┘                   │
  └─────────────────────────────────────────────────────────────┘
```
  Blazor Server 的魔法：
  - Blazor Server Component 運行在 Server 上
  - 它可以直接訂閱 Orleans Stream（因為都在 Server）
  - 當收到 Stream 訊息 → 呼叫 StateHasChanged() → Blazor 自動透過 SignalR 推送 UI 差異到 Browser

  這就是為什麼 ChatRoom sample 看起來像是 Orleans Stream 直接到 Browser，但實際上中間有 Blazor Server 作為橋樑。

  ---
  現在讓我實作 ZestExchange 的即時 OrderBook！

# Dependencies
```
dotnet add ZestExchange.Web/ZestExchange.Web.csproj package Microsoft.Orleans.Client
dotnet add ZestExchange.Web/ZestExchange.Web.csproj reference ZestExchange.Contracts/ZestExchange.Contracts.csproj
// Orlean Streaming
dotnet add ZestExchange.Silo/ZestExchange.Silo.csproj package Microsoft.Orleans.Streaming
dotnet add ZestExchange.Web/ZestExchange.Web.csproj package Microsoft.Orleans.Streaming
```

# Stream Pub/Sub
  
  Silo 的兩個 GrainStorage
```c#
  //Users/zest/Projects/c-net/ZestExchange/ZestExchange.Silo/Program.cs

  // 1. Default - 用來儲存 Grain State（如果有 [PersistentState] 的話）
  siloBuilder.AddMemoryGrainStorage("Default");

  // 2. PubSubStore - Orleans Streams 內部使用，追蹤 "誰訂閱了哪個 Stream"
  siloBuilder.AddMemoryGrainStorage("PubSubStore");
```

  PubSubStore 的作用：
```
  ┌─────────────────────────────────────────────────────────┐
  │                   PubSubStore                            │
  │  ┌─────────────────────────────────────────────────┐    │
  │  │  Stream: "orderbook/BTC-USDT"                    │    │
  │  │  Subscribers:                                    │    │
  │  │    - Blazor Component (WebFrontend)             │    │
  │  │    - (future) Mobile App                        │    │
  │  └─────────────────────────────────────────────────┘    │
  └─────────────────────────────────────────────────────────┘
```
  Orleans Streams 需要知道「誰訂閱了哪個 Stream」，這個資訊就存在 PubSubStore 裡。

  ---
## Web 的 Orleans Client
```c#
// /Users/zest/Projects/c-net/ZestExchange/ZestExchange.Web/Program.cs
  builder.UseOrleansClient(clientBuilder =>
  {
      clientBuilder.UseLocalhostClustering();           // 連接到 Silo
      clientBuilder.AddMemoryStreams("OrderBookProvider"); // 訂閱 Stream
  });
```
```
  流程：
  ┌──────────────┐                    ┌──────────────┐
  │    Silo      │                    │  Web (Blazor)│
  │              │   Orleans Stream   │              │
  │  Grain       │ ─────────────────► │  Client      │
  │  (Publish)   │  "OrderBookProvider" │  (Subscribe) │
  └──────────────┘                    └──────────────┘
```
  完整流程：
  1. Silo 註冊 AddMemoryStreams("OrderBookProvider") → 建立 Stream Provider
  2. MatchingEngineGrain 呼叫 _orderBookStream.OnNextAsync(update) → Publish
  3. Web Client 註冊相同的 AddMemoryStreams("OrderBookProvider") → 可以訂閱
  4. OrderBook.razor 呼叫 stream.SubscribeAsync(OnOrderBookUpdated) → Subscribe

  名稱必須一致："OrderBookProvider" 才能互相溝通！

## Publish current OrderBook state to Orleans Stream

```c#
IAsyncStream<OrderBookUpdated>? _orderBookStream;
await _orderBookStream.OnNextAsync(update);
```


# 問題是 Memory Streams 主要是給 Silo 內部用的，跨進程（Web Client → Silo）需要特殊配置。


  改成 Timer 輪詢（每 500ms）：
  - Polls 計數器會一直增加
  - 下單後不用刷新，會自動更新

  ---
  關於 Orleans Streams 的問題：

  Memory Streams 在 單進程 內運作良好，但跨進程（Web Client → Silo）有限制。

  更好的方案（面試可以講）：
  1. 生產環境：用 Azure Event Hubs / Kafka Stream Provider
  2. 本地開發：Timer 輪詢 或 SignalR Hub
```
                        面試回答
  ┌─────────────────────────────────────────────────────────┐
  │  "我用 Memory Streams 做 MVP，生產環境會換成          │
  │   Event Hubs，因為 Memory Streams 不支援跨進程訂閱"   │
  └─────────────────────────────────────────────────────────┘

```

## ChatRoom - Orleans Stream 可以運作
```
  ┌─────────────────────────────────────────────────────────┐
  │  Client 是 Console App (Orleans Client)                 │
  │                                                         │
  │  Console App ──────────────────────► Orleans Cluster    │
  │  (Orleans Client)    直接連接         (Silo)           │
  │       │                                                 │
  │       └── stream.SubscribeAsync() 在同一個 Orleans 網路內│
  └─────────────────────────────────────────────────────────┘
```  
Console App 是 Orleans Client，可以直接訂閱 MemoryStreams！

在官方最新的 ChatRoom 範例中，他們其實是把 Web App 和 Silo  Co-hosting (共存) 在同一個 Process 裡的！



## GPSTracker - Browser 不能直接訂閱 Orleans Streams
```
  ┌─────────────────────────────────────────────────────────┐
  │  Browser 不是 Orleans Client！                          │
  │                                                         │
  │  DeviceGrain → PushNotifierGrain → RemoteLocationHub    │
  │                                           ↓             │
  │                                    SignalR Hub          │
  │                                           ↓             │
  │                                       Browser           │
  └─────────────────────────────────────────────────────────┘
```
  GPSTracker 用 SignalR 作為橋樑！不是直接用 Orleans Streams！


## 我們的架構瓶頸
   * 你的 Silo 在 Process A。
   * 你的 Web (Blazor) 在 Process B。
   * 你想讓 Process B 訂閱 Process A 的事件。
這必須透過網路 (Network)。Memory 是不通的，必須要有一個 中間人 (Broker) 或是 直接的 RPC 呼叫。


結論：你的架構與 ChatRoom 不同

* ChatRoom: 單體式架構 (Monolith)。Web 和 Orleans Silo
    都在同一個 .exe 裡跑。
* ZestExchange: 微服務架構 (Microservices)。
    * ZestExchange.Silo 是一個 Process (Docker Container)。
    * ZestExchange.Web 是另一個 Process (Docker Container)。

在你的架構下，MemoryStream 絕對無法跨越這兩個 Container。

## 方案

### 方案 B: SignalR Bridge (GPSTracker 模式)
````
  ┌─────────────────────────────────────────────────────────────────┐
  │              ZestExchange.Silo (需要加 SignalR)                  │
  │                                                                  │
  │  MatchingEngineGrain                                            │
  │       │                                                          │
  │       │ PlaceOrder → 撮合完成                                    │
  │       ▼                                                          │
  │  直接呼叫 IHubContext<OrderBookHub>                              │
  │       │                                                          │
  │       │ Clients.All.SendAsync("OrderBookUpdated", snapshot)     │
  │       ▼                                                          │
  │  SignalR Hub (OrderBookHub) ─────────────────────────────────────┼──► WebSocket
  │                                                                  │
  └─────────────────────────────────────────────────────────────────┘
                                                                     │
                                                                     ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │              ZestExchange.Web (純 Blazor)                        │
  │                                                                  │
  │  ❌ 不需要 UseOrleansClient()                                    │
  │  ✅ 用 HubConnection 連到 Silo 的 SignalR                        │
  │                                                                  │
  │  hubConnection.On("OrderBookUpdated", data => { ... })          │
  └─────────────────────────────────────────────────────────────────┘
```
  問題：GPSTracker 的 Silo 和 Web 是同一個進程。我們的是分開的，所以：
  - 需要在 Silo 開一個 HTTP endpoint 給 SignalR
  - 或者把 Silo 改成 ASP.NET Core Host（同時跑 Orleans + SignalR）

  ---
 ### 方案 C: Redis Streams (Gemini 建議)
```
  ┌─────────────────────────────────────────────────────────────────┐
  │                        Redis (Aspire 加一行)                     │
  │                                                                  │
  │                    ┌─────────────────────┐                      │
  │                    │   Redis Streams     │                      │
  │                    │   "orderbook"       │                      │
  │                    └─────────────────────┘                      │
  │                         ▲           │                           │
  │                         │           │                           │
  │           Publish       │           │  Subscribe                │
  └─────────────────────────┼───────────┼───────────────────────────┘
                            │           │
           ┌────────────────┘           └────────────────┐
           │                                             │
  ┌────────┴────────┐                         ┌──────────┴────────┐
  │ ZestExchange.Silo│                         │ ZestExchange.Web  │
  │                  │                         │                   │
  │ UseOrleans()     │                         │ UseOrleansClient()│
  │ AddRedisStreams()│                         │ AddRedisStreams() │
  │                  │                         │                   │
  │ Grain publishes  │                         │ Blazor subscribes │
  │ to Redis Stream  │                         │ to Redis Stream   │
  └──────────────────┘                         └───────────────────┘
```
  優點：
  - Orleans Streams API 不變（只改 Provider）
  - 你目前的 Blazor 代碼幾乎不用改
  - 跨進程 ✅
  - 生產環境 Ready ✅
  - 面試加分（Redis Message Broker）