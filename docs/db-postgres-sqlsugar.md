# Postgresql
https://www.youtube.com/watch?v=OGVE4vnSMuA&t=33s
https://www.youtube.com/watch?v=06qylcEJP-k&t=157s


# SqlSugar outline

✦ 這是一個關於 ORM 選擇的好問題。在 .NET 生態系中，EF Core 是主流，但 SqlSugar
  (來自中國開發者) 擁有非常忠實的擁護者群體。

  以下是 SqlSugar 的主要特性與語法風格，以及它為什麼可能適合你的場景：

  1. 語法特性：直覺、類似 SQL

  SqlSugar 最大的賣點是它的語法設計非常「貼心」，很多複雜的查詢寫起來比 EF Core 簡潔。

  範例：你的 Market Stats 查詢

   * EF Core (Linq):
```c#
   1     var stats = await db.Trades
   2         .Where(t => t.Symbol == symbol && t.Time >= DateTime.Now.AddHours(-24))
   3         .GroupBy(t => t.Symbol)
   4         .Select(g => new {
   5             High = g.Max(t => t.Price),
   6             Low = g.Min(t => t.Price),
   7             Vol = g.Sum(t => t.Quantity)
   8         })
   9         .FirstOrDefaultAsync();
```
      EF Core 強調強型別，但在做聚合查詢 (Aggregation) 時，語法有時會顯得冗長。

   * SqlSugar:
```c#
   1     var stats = await db.Queryable<TradeHistory>()
   2         .Where(t => t.Symbol == symbol && t.Time >= DateTime.Now.AddHours(-24))
   3         .Select(t => new {
   4             High = SqlFunc.AggregateMax(t.Price),
   5             Low = SqlFunc.AggregateMin(t.Price),
   6             Vol = SqlFunc.AggregateSum(t.Quantity)
   7         })
   8         .FirstAsync();
```
      SqlSugar 的 SqlFunc 類別提供了很多直接對應 SQL 函數的方法，這對習慣寫 SQL
  的人來說非常親切。

  2. 特色功能 (Killer Features)

   * 自動分表 (Sharding)：這是 SqlSugar 最強的功能之一。對於交易所來說，成交紀錄
     (TradeHistory) 成長速度極快。SqlSugar
     支援「自動按年/月/日分表」，而且查詢時可以跨表查詢，這點 EF Core
     目前做不到（需要手動處理或依賴資料庫端 Partitioning）。
       * 應用場景：你可以設定 TradeHistory 每年存一張表 (TradeHistory_2024,
         TradeHistory_2025)，SqlSugar 幫你自動路由。
   * 多資料庫支援：一套代碼可以在 MySQL, PostgreSQL, SqlServer, Oracle, SQLite
     之間無縫切換。
   * Bulk Copy (高效能插入)：它的 Fastest<T>().BulkCopy(list) 效能號稱比 EF Core 的
     AddRange 快很多，非常適合你這種「每秒幾千筆成交」的寫入場景。

  3. 為什麼選 SqlSugar？

   * 優點：
       1. 效能優化：針對大量數據寫入和查詢有特別優化。
       2. 語法糖：很多 SQL 語法糖 (如 SqlFunc.DateIsSame) 讓開發更快速。
       3. 輕量級：比 EF Core 輕量，啟動速度快。

   * 缺點 (考慮點)：
       1. Aspire 整合：Aspire 官方範例都是 EF Core，用 SqlSugar 需要自己處理 Connection
          String 和 DI 注入（不過這不難）。
       2. 生態系：雖然在中文圈很紅，但在全球 .NET 社群中，EF Core 資源還是壓倒性多。

  結論：
  既然你想「測試
  SqlSugar」，而且你的專案是「交易所撮合」（高併發寫入、大數據量），SqlSugar 的 Bulk
  Copy 和 分表功能 其實是非常適合這個場景的！

  我們可以就用 SqlSugar 來挑戰看看！

# Postgresql volumn

  現況

  OrderBookEngine (In-Memory)
  ├── SortedDictionary<price, LinkedList<Order>> _bids/_asks  ← 撮合用
  ├── Dictionary<Guid, OrderLocation> _orderLookup            ← O(1) 取消
  └── 重啟後資料全部消失

  要存什麼到 PostgreSQL？

  | 資料            | 用途           | 建議              |
  |-----------------|----------------|-------------------|
  | Orders          | 訂單歷史查詢   | ✅ 必存           |
  | Trades          | 成交紀錄、報表 | ✅ 必存           |
  | OrderBook State | 重啟後恢復     | ❌ 從 Orders 重建 |

  架構選項

  Option A: Write-Through (推薦)

  PlaceOrder Request
         │
         ▼
  ┌──────────────────┐     ┌─────────────────┐
  │ MatchingEngine   │────►│   PostgreSQL    │
  │    (In-Memory)   │     │  Orders/Trades  │
  │   高速撮合       │     │    持久化        │
  └──────────────────┘     └─────────────────┘
         │
         ▼
    Orleans Stream → UI

  - Write: 每次下單/成交 → 同時寫 DB
  - Read: OrderBook 還是 in-memory (撮合需要高速)
  - Startup: 從 DB 載入 pending orders 重建 OrderBook

  Option B: Event Sourcing (進階)

  PlaceOrder → Store Event → Rebuild State

  - 更複雜，適合需要完整 audit trail 的場景

  ---
##  SqlSugar vs EF Core
```
  | 特性      | SqlSugar       | EF Core |
  |-----------|----------------|---------|
  | 學習曲線  | 低             | 中      |
  | 中國社群  | 強             | 普通    |
  | 性能      | 較好           | 普通    |
  | Migration | 有 (CodeFirst) | 強      |
  | LINQ      | 支援           | 完整    |
```
  SqlSugar 適合你的場景，輕量且快。

  ---
  建議的 Table Schema

### 訂單表
```sql
  CREATE TABLE orders (
      id UUID PRIMARY KEY,
      symbol VARCHAR(20) NOT NULL,
      side SMALLINT NOT NULL,        -- 0=Buy, 1=Sell
      type SMALLINT NOT NULL,        -- 0=Limit, 1=Market
      price DECIMAL(18,8) NOT NULL,
      quantity DECIMAL(18,8) NOT NULL,
      filled_quantity DECIMAL(18,8) DEFAULT 0,
      status SMALLINT NOT NULL,      -- 0=New, 1=PartiallyFilled, 2=Filled, 3=Cancelled
      created_at TIMESTAMP NOT NULL,
      updated_at TIMESTAMP NOT NULL
  );
```
### 成交表
```sql
  CREATE TABLE trades (
      id UUID PRIMARY KEY,
      symbol VARCHAR(20) NOT NULL,
      maker_order_id UUID NOT NULL,
      taker_order_id UUID NOT NULL,
      price DECIMAL(18,8) NOT NULL,
      quantity DECIMAL(18,8) NOT NULL,
      executed_at TIMESTAMP NOT NULL
  );

  -- Index for queries
  CREATE INDEX idx_orders_symbol_status ON orders(symbol, status);
  CREATE INDEX idx_trades_symbol_time ON trades(symbol, executed_at DESC);
```
  ---
  問題討論

  1. 撮合還是要 in-memory - DB 太慢，撮合引擎不能每次都查 DB
  2. Write-through 延遲 - 寫 DB 會增加延遲，可用 async write (fire-and-forget) 或 batch write
  3. 重啟恢復 - Grain 啟動時從 DB 載入 status = New or PartiallyFilled 的訂單

  你想怎麼處理寫入延遲？
  - A) 同步寫 - 確保一致性，但較慢
  - B) 異步寫 - 高速，但有極小機率丟失 (crash 時)
  - C) Batch 寫 - 累積 N 筆再寫，平衡性能和一致性

# connect to postgresql

✦ 從截圖中，我看到了 PostgreSQL 的資源，但狀態是 Starting 或者是剛啟動。

  要查看資料庫裡面的資料，有幾種方法：

  方法 1：使用 Aspire Dashboard (最簡單)
  Aspire 預設整合了一個資料庫瀏覽器 (Database Explorer)。
   1. 在 Dashboard 的 Resources 頁面。
   2. 找到 postgres 資源。
   3. 看右邊的 Endpoints 欄位，點擊對應的連結（通常是 localhost:xxxx）。
   4. 有時候 Aspire 會直接在資源行顯示一個 "Database Explorer"
      的圖示或連結，點進去就可以直接下 SQL 查詢。

  方法 2：使用外部 GUI 工具 (如 pgAdmin, DataGrip, TablePlus)
  這是開發者最常用的方式：
   1. 取得連線資訊：在 Dashboard 中點選 postgres 資源，查看 Environment Variables
      (環境變數)。
       * 找 POSTGRES_USER (預設可能是 postgres)。
       * 找 POSTGRES_PASSWORD (Aspire 通常會自動產生一串隨機字串)。
   2. 取得 Port：看 postgres 的 Endpoints，會有一個對應到內部 5432 的外部 Port。
   3. 連線：使用上述資訊連入 localhost:<外部Port>，Database 名稱選 exchangedb。

  方法 3：使用 Docker CLI (最硬核)
  在終端機直接下指令進去查：
```
   1 # 1. 找到容器 ID (例如名為 zestexchange-postgres)
   2 docker ps
   3
   4 # 2. 進入容器並執行 psql
   5 docker exec -it <容器ID> psql -U postgres -d exchangedb
   6
   7 # 3. 查詢資料
   8 SELECT * FROM trade_history LIMIT 10;
```
  方法 4：直接看 Web Frontend
  如果你懶得下 SQL，直接看你的 /orderbook 頁面：
   * 如果 Market Stats (24h High/Low)
     有數字出現，那代表資料庫一定有資料了！因為那邊的數據是我們剛寫的
     TradeRepo.Get24hStatsAsync 從 PostgreSQL 算出來的。

# Stat 24hr
OrderBook.razor, Pollin DB - TradeRepo.Get24hStatsAsync