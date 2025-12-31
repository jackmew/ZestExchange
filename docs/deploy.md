# redis -> before deploy to azure

 1. 本機開發 (Localhost Clustering)
  當你在本機跑 UseLocalhostClustering 時，Orleans 做了一個非常簡單粗暴的假設：
   * Silo: 我是老大，我就在 127.0.0.1:11111。
   * Client: 老大就在 127.0.0.1:30000，我去連它。

  這在 單機單進程 (Single Machine, Single Process) 的環境下完全沒問題。

  2. 雲端環境 (Azure Container Apps / Kubernetes)
  當你部署到雲端時，情況變了：
   * Silo 跑在容器 A (IP: 10.0.0.5)。
   * Web 跑在容器 B (IP: 10.0.0.6)。
   * ApiService 跑在容器 C (IP: 10.0.0.7)。

  這時候如果你還用 Localhost (127.0.0.1)：
   * Web 容器會嘗試連線 127.0.0.1:30000 (也就是連它自己)，結果當然什麼都沒有，因為 Silo
     在隔壁那台機器上。

  3. Redis 的角色：通訊錄 (Membership Table)
  這時候我們需要一個 「所有人都看得到的佈告欄」。

   1. Silo 啟動時：它會連上 Redis，寫下一行字：
      > "我是 Silo A，我的 IP 是 10.0.0.5，我還活著。"
   2. Web 啟動時：它不知道 Silo 在哪，於是它去問 Redis：
      > "現在有哪些 Silo 活著？"
   3. Redis 回答：
      > "有個叫 Silo A 的在 10.0.0.5。"
   4. Web 連線：Web 拿到 IP 後，直接去連 10.0.0.5。

  這就是為什麼我們需要 Redis。它充當了 Membership Table (成員名單)
  的角色，讓在這個動態變化的雲端環境中，服務可以互相找到對方。

  總結：
   * Localhost: 假設大家都在同一個房間 (127.0.0.1)。
   * Redis: 讓大家在茫茫網海中 (Cloud Network) 透過查閱通訊錄找到彼此。

  這就是為什麼為了上雲，我們必須引入 Redis（或 Azure Table Storage）作為這個共用的通訊錄。

# var connectionString = builder.Configuration.GetConnectionString("redis");

  1. 關於 AppHost 裡的 Redis
   1 // AppHost/Program.cs
   2 var redis = builder.AddRedis("redis");
  這行代碼告訴 Aspire：「我要一個 Redis 資源」。
   * 在本機開發時：Aspire 會啟動一個 Redis Docker Container。
   * 在 Azure 時 (`azd up`)：Aspire 會在 Azure 上申請一個 Azure Cache for Redis。

  關鍵點：無論是開發還是生產環境，這個 Redis 資源都會產生一個 連線字串 (Connection String)。

  2. 關於 WithReference(redis)
   1 .WithReference(redis)
  這行代碼的作用是：把 Redis 的連線字串，透過 環境變數 注入到目標專案（例如 ApiService）中。
  環境變數的名稱通常是 ConnectionStrings__redis。

  3. 關於 if (string.IsNullOrEmpty(connectionString)) 的判斷
  這個判斷是為了 相容性 (Backward Compatibility) 和 靈活性。
```c#
    1 // ApiService/Program.cs
    2 var connectionString = builder.Configuration.GetConnectionString("redis");
    3
    4 if (string.IsNullOrEmpty(connectionString))
    5 {
    6     // A 計畫：沒給 Redis 就用 Localhost (簡易模式)
    7     clientBuilder.UseLocalhostClustering();
    8 }
    9 else
   10 {
   11     // B 計畫：給了 Redis 就用 Redis (標準模式)
   12     clientBuilder.UseRedisClustering(...);
   13 }
```
  這個判斷判斷的是什麼？
  它在判斷：「現在是誰在啟動我？」

   * 情況 1：被 Aspire 啟動 (標準模式)
       * 你跑 dotnet run --project AppHost。
       * Aspire 啟動 Redis -> 產生連線字串 -> 注入環境變數。
       * GetConnectionString("redis") 有值。
       * 程式走入 else 分支 -> 使用 Redis Clustering。

   * 情況 2：單獨啟動 (簡易模式)
       * 假設有一天，你不想開 Docker，也不想跑 Aspire，你只想快速測試一下 API。
       * 你在終端機直接跑 dotnet run --project ApiService。
       * 這時候沒有人注入 Redis 連線字串。
       * GetConnectionString("redis") 是空的 (null)。
       * 程式走入 if 分支 -> Fallback 到 Localhost Clustering。
       * (當然，這時候你也必須手動跑一個 Silo 才能通)。

  結論：
  這個判斷是為了讓你的程式碼更強健。它既能在 Aspire 的管理下運作（連 Redis），也能在脫離 Aspire
  的情況下運作（連 Localhost）。這是一種防禦性程式設計。

  # azd up

    當你執行 azd up 時，我們現在正處於「層級 2：Azure Container Apps (ACA)」。

  azd up 實際上做了什麼？

  對於你的 .NET Aspire 專案，azd 會進行以下操作：

   1. 基礎設施 (Infrastructure)：
       * 它會在 Azure 上建立一個 Managed Environment (其實就是一個隱藏的 K8s Cluster)。
       * 它會建立 Azure Container Apps (對應你的 webfrontend, apiservice, silo, load-btc, load-eth)。
       * 它會建立 Azure Cache for Redis (用來做 Orleans Clustering)。

   2. 為什麼這不是 HFT 等級？
       * 資源共享：ACA 的底層機器是共享的 (Multi-tenant)。你的撮合引擎 (Silo)
         可能跟別人的部落格跑在同一台實體機上。如果別人的部落格突然爆紅，CPU 衝高，你的 L3 Cache
         可能會被洗掉，導致撮合延遲從 50us 跳到 5ms (抖動)。
       * 網路堆疊：ACA 使用了 Envoy 作為 Sidecar Proxy。所有的進出流量都要經過 Envoy。這對 Web API
         很好，但對 Orleans 的 Silo-to-Silo 通訊來說，多了一層代理就是多了一層延遲。

  對於你的 ZestExchange 專案

   * 現階段 (Prototype / MVP)：
      使用 ACA (azd up) 是最正確的選擇。因為它能讓你用最少的時間驗證業務邏輯
  (撮合、串流、UI)。你不需要花一個月去搞定 K8s YAML。

   * 未來階段 (Production HFT)：
      如果有一天你的交易所真的要上線跟幣安拚速度，你就必須離開 ACA，轉向 AKS (Azure Kubernetes
  Service)。
       * 你會使用 Node Pools 設定 Dedicated Host。
       * 你會使用 Headless Service 讓 Silo 直接對話 (繞過 Proxy)。
       * 你會使用 Proximity Placement Groups 讓所有 Silo 都在同一個機房機櫃裡，甚至同一個機架上。

  結論

  現在按下 azd up 是為了驗證架構和展示成果。這是展示 Orleans 威力最快的方式。

  至於那最後 1% 的極致效能優化 (K8s / Dedicated Host)，那是等到你有 10
  萬個用戶、每秒幾萬筆交易時才需要煩惱的 "Happy Problem"。

  現在，我們可以先享受 ACA 帶來的便利，一鍵擁有一個雲端交易所。 🚀

  # azd deploy

  ✦ 這就是 azd 最好用的地方。部署後如果你改了代碼，你不需要重新執行整個漫長的流程。

  根據你的修改內容，有兩種選擇：

  1. 只改了「程式碼」(Code Only)
  如果你只是改了 .cs 檔案或 .razor 介面，基礎設施 (Redis, Network) 都沒變：
  執行：
   1 azd deploy
   * 發生什麼事：它只會重新 Build 你的 Docker Images，推送到 ACR，然後觸發 Container Apps 滾動更新
     (Rolling Update)。
   * 速度：非常快，通常 3 - 5 分鐘 搞定。

  2. 改了「架構」或「設定」(Infrastructure Change)
  如果你在 AppHost 裡加了新專案、改了環境變數、或是加了個資料庫：
  執行：
   1 azd up
   * 發生什麼事：它會先跑 azd provision (更新 Azure 上的資源設定)，再跑 azd deploy。
   * 速度：比單純 deploy 慢一點，但它很聰明，只會更新「有變動」的部分。

  3. 如果只是想快速測試本機
  別忘了，你還是可以像平常一樣執行：
   1 dotnet run --project ZestExchange.AppHost
  這會在你的 Mac 上執行，完全不影響 Azure 上的版本。等到本機測好了，再跑 azd deploy 推上去。

  常用指令總結：
   * azd up：我想要「同步」所有東西 (基礎設施 + 程式碼)。
   * azd deploy：我只想把最新的「程式碼」推上去。
   * azd provision：我只想在 Azure 上「開資源」，還不想推程式碼。

  要開始執行第一次的 `azd up` 了嗎？ 我們來看看你的雲端交易所誕生！ 🚀


# azd deploy done

Deploying services (azd deploy)

  (✓) Done: Deploying service apiservice
  - Endpoint: https://apiservice.internal.wittycoast-fc858374.japaneast.azurecontainerapps.io/

  (✓) Done: Deploying service load-btc

  (✓) Done: Deploying service load-eth

  (✓) Done: Deploying service redis
  - Endpoint: https://redis.internal.wittycoast-fc858374.japaneast.azurecontainerapps.io/

  (✓) Done: Deploying service silo

  (✓) Done: Deploying service webfrontend
  - Endpoint: https://webfrontend.wittycoast-fc858374.japaneast.azurecontainerapps.io/

  Aspire Dashboard: https://aspire-dashboard.ext.wittycoast-fc858374.japaneast.azurecontainerapps.io

SUCCESS: Your application was deployed to Azure in 3 minutes 55 seconds.
You can view the resources created under the resource group rg-zestexchange in Azure Portal:
https://portal.azure.com/#@/resource/subscriptions/8428e473-e5fb-4a1b-9c8b-1305d4420532/resourceGroups/rg-zestexchange/overview

