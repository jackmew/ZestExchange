
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