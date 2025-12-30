
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
```
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


# ❌ Aspire 的網路環境與單純的 Localhost 不同

  ChatRoom 的 Client 和 Server 確實是兩個獨立的 Process
  (ChatRoom.Client.exe 和 ChatRoom.Service.exe)。

  那麼 MemoryStream 怎麼跨過去的？

  答案：它並沒有跨過去，而是透過 TCP 傳送了！

  這是我之前解釋的一個修正。在 Orleans
  的舊版本（以及特定配置下），Client 連接 Silo 時：
   1. Client 初始化: clientBuilder.UseLocalhostClustering()。
       * 這一步建立了 Client 到 Silo 的 TCP 長連線 (Orleans
         Gateway Protocol)。
   2. 訂閱 Stream:
       * Client 呼叫 stream.SubscribeAsync(observer)。
       * Client 透過 TCP 告訴 Silo：「嘿，我有個 Observer
         要聽這個 Stream」。
       * Silo 的 PubSubStore (Memory) 記錄下：「有一個遠端的
         Observer (Client) 訂閱了」。
   3. 發送訊息:
       * Grain (Silo) 呼叫 OnNextAsync。
       * Silo 查 PubSubStore，發現有一個訂閱者是「遠端 Client」。
       * Silo 透過 TCP 把訊息序列化，丟給 Client。
       * Client 收到 TCP 封包，反序列化，觸發
         StreamObserver.OnNextAsync。

  為什麼我之前說 MemoryStream 不能跨進程？
  因為這僅限於 Orleans Client <-> Orleans Silo 這種原生連線。
   * ChatRoom Client 是真正的 Orleans Client (它跑了 Orleans 的
     Client SDK)。
   * 你的 Blazor Web 如果也跑了
     UseOrleansClient，理論上也能做到一樣的事！

  但是！這裡有一個巨大的坑 (The Catch)：
  Aspire 的網路環境與單純的 Localhost 不同。
   * ChatRoom 範例通常是在本機直接跑兩個 EXE，網路直通。
   * Aspire 跑在 Docker Container 裡，Client 連 Silo 常常會因為
     IP Address / Port Mapping / Docker Network 的問題，導致 TCP
     連線建立成功但「回呼 (Callback)」失敗，或者 PubSubStore
     記錄的 IP 是 Container 內網 IP，Client 根本連不到。

  這就是為什麼在 Container/K8s
  環境下，我們強烈不建議依賴這種「原生 TCP Stream」，而推薦使用
  External Broker (Redis/RabbitMQ)。因為 External Broker
  只要連線字串對了就能通，完全不管你 Orleans 內部的複雜網路拓樸。

  # AddProject 在本機開發時 可能是可以的

  ✦ 是的，你可以這樣用，而且在你的目前設定下是完全可行的。

  你的 ZestExchange 專案目前配置方式 (builder.AddProject) 讓你在本機開發時，幾乎等同於 ChatRoom
  的執行環境。

  為什麼這在你的 Aspire 專案中行得通？

  你在 Aspire AppHost 中使用的是 AddProject，而不是 AddContainer：
```c#
   1 // ZestExchange.AppHost/Program.cs
   2 var silo = builder.AddProject<Projects.ZestExchange_Silo>("silo");
   3 var web = builder.AddProject<Projects.ZestExchange_Web>("webfrontend");
```
  這意味著當你按下 F5 (Debug) 時：
   1. Silo 是一個在你的 Mac 上直接執行的 Process (就像 ChatRoom.Service.exe)。
   2. Web (Blazor) 也是一個在你的 Mac 上直接執行的 Process (就像 ChatRoom.Client.exe)。
   3. 它們共享 localhost 網路環境。

  所以你提到的 Delivery (The Network) 流程在你的專案中是這樣跑的：

   1. Silo (Grain): 呼叫 stream.OnNextAsync(orderBookUpdate)。
   2. Silo (Runtime): 查表發現 ZestExchange.Web 這個 Process 訂閱了。
   3. Network: Silo 透過 TCP (Localhost Port 30000) 把序列化後的 Bytes 傳給 ZestExchange.Web。
   4. Web (Orleans Client): 收到 Bytes -> 反序列化 -> 觸發 StreamObserver。
   5. Web (Blazor Server): 你的 C# 程式碼收到通知，更新 UI 變數。
   6. SignalR (Web Socket): Blazor 自動計算 DOM 差異，透過 SignalR 推送到使用者的 瀏覽器 (Browser)
      更新畫面。

  關鍵區別 (The Catch)

  雖然現在能跑，但你要清楚這跟 GPSTracker 的差別：

   * GPSTracker: 瀏覽器直接透過 SignalR 收資料 (Browser <-> Server)。
   * 你的 ZestExchange (Blazor Server): 是 Web Server (後端) 在當 Orleans Client。瀏覽器並沒有直接連
     Silo。

  總結

  你目前的寫法 (MemoryStream + LocalhostClustering) 在 Aspire 本機開發 (AddProject) 是沒問題的。

## ❌  但是，如果你未來要將這個專案部署到： Aspire 會幫你注入環境變數，你只要把 UseLocalhostClustering 換成UseClustering(...) 即可
   1. Docker Compose (手動跑 docker-compose up)
   2. Kubernetes (K8s)
   3. Azure Container Apps

  那時候 UseLocalhostClustering 就會失效，因為在容器世界裡 localhost
  指的是容器自己，不是別的容器。那時候你就需要改用 Orleans.Clustering.AzureStorage 或 Redis 來做
  Clustering，並且 Streams 建議換成 AddRedisStreams 以確保穩定性。

  結論：現在保持這樣寫沒問題，這是最快能看到成果的方式。


  # ✅ 真正原因 + @rendermode InteractiveServer

✦ 你發現了問題的關鍵！

  問題原因：
  你的 OrderBook.razor 缺少了 Render Mode 的設定。
  在 .NET 8 Blazor 中，預設是 Static Server Rendering (SSR)。這意味著：
   1. 使用者請求頁面 -> Server 執行 OnInitialized -> 產生 HTML -> 回傳給瀏覽器 -> 斷線。
   2. 雖然你的 Code 有 Stream.Subscribe，但因為 HTTP 請求已經結束，這個 Component 隨即被
      Dispose，而且也沒有 SignalR 連線可以讓你 InvokeAsync(StateHasChanged) 推送更新回瀏覽器。

  修正：
  我已經幫你在 OrderBook.razor 加上了 @rendermode InteractiveServer。

   1 @page "/orderbook"
   2 @rendermode InteractiveServer  <-- 加上這行
   3 @implements IAsyncDisposable
   4 ...

  現在的運作流程：
   1. Prerendering (SSR): Server 快速產生初始 HTML (看到 loading 或初始數據)。
   2. Hydration: 瀏覽器載入 blazor.web.js，建立 SignalR (WebSocket) 連線回 Server。
   3. Interactive Mode: OrderBook Component 再次在 Server 記憶體中啟動，並透過 SignalR 保持連線。
   4. Real-time Update:
       * Orleans Stream 收到新報價。
       * OnOrderBookUpdated 被觸發。
       * InvokeAsync(StateHasChanged) 透過這條 SignalR 連線，告訴瀏覽器 "DOM 這裡變了，請重繪"。

# Blazor 預設就有AddSignalR

✦ 這是一個很好的觀察！答案是：你雖然沒有手動寫 `AddSignalR()`，但 Blazor Server 的「底層」就是
  SignalR。

  這就是為什麼你在程式碼中看不到 Hub，卻能實現即時更新的原因。

  1. 誰幫你加了 SignalR？
  在你的 ZestExchange.Web/Program.cs 中，這兩行程式碼就是關鍵：
```c#
   1 // 這行在註冊服務時，背後自動啟動了 SignalR 服務
   2 builder.Services.AddRazorComponents()
   3     .AddInteractiveServerComponents();
   4
   5 // 這行在中間件中，啟動了 Blazor 與瀏覽器之間的 SignalR 端點
   6 app.MapRazorComponents<App>()
   7     .AddInteractiveServerRenderMode();
```
  2. Blazor Server 的運作原理
  當你使用 @rendermode InteractiveServer 時：
   1. 建立連線：瀏覽器一開啟，就會自動連上一條 SignalR (WebSocket) 長連線到你的 Web Server。
   2. 邏輯在 Server：你的 OrderBook.razor 實例其實是跑在 Web Server 的記憶體裡，而不是瀏覽器。
   3. 觸發更新：
       * 當 Orleans Stream 傳來新報價，執行了 InvokeAsync(StateHasChanged)。
       * Blazor Server 會在伺服器端計算「新的 HTML」與「舊的 HTML」差在哪裡（這叫 Diffing）。
       * 魔法發生處：伺服器透過那條 自動建立的 SignalR 連線，把這一小塊「差異資料」丟給瀏覽器。
       * 瀏覽器裡的 blazor.web.js 收到後，立刻更新 DOM。

  3. 與 GPSTracker 的區別
   * GPSTracker: 它是「手動 SignalR」。程式員要自己定義 Hub，自己寫 SendAsync 傳送 JSON，前端也要自己寫
     JavaScript 去接資料。
   * ZestExchange (Blazor): 它是「自動 SignalR」。你只需要改 C# 的變數（例如 _bids），Blazor 就會利用
     SignalR 幫你把畫面同步過去。你完全不需要處理 JSON 序列化或網路通訊協定。

  總結：
  你不需要手動加 SignalR，因為你選用了 Blazor
  Server。它幫你把最困難的「伺服器主動推播到瀏覽器」這部分封裝好了。你只需要關注 C# 邏輯就好！


# 真正的架構（不需要自己建 SignalR）
```
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │                                                                              │
  │  ZestExchange.Silo                     ZestExchange.Web                      │
  │  ┌─────────────────┐                   ┌─────────────────────────────────┐  │
  │  │                 │                   │                                 │  │
  │  │ MatchingEngine  │   Orleans         │  UseOrleansClient()             │  │
  │  │ Grain           │   Protocol        │       ↓                         │  │
  │  │      │          │ ──────────────►   │  Orleans Client 訂閱 Stream     │  │
  │  │      ▼          │   (TCP 30000)     │       ↓                         │  │
  │  │ Stream.OnNext() │                   │  OnOrderBookUpdated() 被觸發    │  │
  │  │                 │                   │       ↓                         │  │
  │  └─────────────────┘                   │  StateHasChanged()              │  │
  │                                        │       ↓                         │  │
  │                                        │  Blazor 內建 SignalR ──────────────┼──► Browser
  │                                        │  (自動的！不用自己建)           │  │
  │                                        │                                 │  │
  │                                        └─────────────────────────────────┘  │
  │                                                                              │
  └─────────────────────────────────────────────────────────────────────────────┘
```
  關鍵理解

  | 層級          | 技術                   | 誰建的？           |
  |---------------|------------------------|--------------------|
  | Silo ↔ Web    | Orleans Protocol (TCP) | UseOrleansClient() |
  | Web ↔ Browser | SignalR WebSocket      | Blazor 內建！      |

  @rendermode InteractiveServer 啟用了 Blazor 內建的 SignalR 連線！

  所以：
  - ✅ Orleans Streams 本來就可以跨進程（透過 Orleans Protocol）
  - ❌ 但沒有 InteractiveServer，Blazor 不會維持 SignalR 連線到 Browser
  - ✅ 加上 InteractiveServer 後，StateHasChanged() 就能透過 Blazor 內建 SignalR 推送到 Browser

  Orleans Streams 一直都是通的，只是 Blazor 的最後一哩路斷了！