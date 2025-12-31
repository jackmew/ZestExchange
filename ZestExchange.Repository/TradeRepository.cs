using SqlSugar;
using ZestExchange.Repository.Entities;

namespace ZestExchange.Repository;
/*
  1. TradeRepository.cs 是什麼角色？
  它是一個 Repository (倉儲層)。
   * 職責：它的唯一工作是「跟資料庫講話」。它封裝了所有的 SQL 邏輯（Insert, Select,
     Aggregate）。
   * 好處：你的業務邏輯（Silo, Web）不需要知道這底下是用 SqlSugar、EF Core 還是手寫
     SQL。它們只要呼叫 InsertAsync 或 GetStatsAsync 就好。
*/
public class TradeRepository
{
    private readonly ISqlSugarClient _db;

    /*
      2. ISqlSugarClient _db 是什麼？
        是的，這就是標準的 依賴注入 (Dependency Injection)。
        * 建構子注入：public TradeRepository(ISqlSugarClient db)
        * 原理：當你在 Extensions.cs 裡寫了 services.AddScoped<ISqlSugarClient>(...)
            後，ASP.NET Core 的容器就知道如何產生一個 ISqlSugarClient。
        * 流程：當你需要 TradeRepository 時，容器會先幫你產生一個 ISqlSugarClient
            (連好資料庫)，然後把它塞進 TradeRepository 的建構子裡，最後把產生好的
            TradeRepository 交給你。
    */
    public TradeRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>
    /// Initialize table if not exists (Code First)
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        _db.CodeFirst.InitTables<TradeHistory>();
    }

    public async Task InsertAsync(TradeHistory trade)
    {
        await _db.Insertable(trade).ExecuteCommandAsync();
    }

/*
   * 傳統 Insert:
   1     INSERT INTO trades VALUES (...); -- 1
   2     INSERT INTO trades VALUES (...); -- 2
   3     INSERT INTO trades VALUES (...); -- 3
      每一筆都要網路來回一次，慢。
   * Bulk Insert (批次插入):
        它利用資料庫的特殊機制（如 PostgreSQL 的 COPY
    指令），把一大坨資料打包成一個二進位串流，一次性塞進資料庫。
    * 情境：當一筆訂單同時成交了 10 筆 Trade 時，用這個方法可以瞬間寫入，比迴圈 Insert
        快幾十倍。
*/
    public async Task BulkInsertAsync(List<TradeHistory> trades)
    {
        if (trades.Count == 0) return;
        // Use Fastest for high performance bulk insert
        await _db.Fastest<TradeHistory>().BulkCopyAsync(trades);
    }

    public async Task<MarketStats?> Get24hStatsAsync(string symbol)
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddHours(-24);

        // SQL:
        // SELECT 
        //   MAX(price) as High, 
        //   MIN(price) as Low, 
        //   SUM(quantity) as Vol,
        //   (SELECT price FROM trade_history WHERE symbol=@s ORDER BY executed_at DESC LIMIT 1) as Last
        // FROM trade_history WHERE symbol=@s AND executed_at >= @y

        // Using SqlSugar Syntax
        var stats = await _db.Queryable<TradeHistory>()
            .Where(t => t.Symbol == symbol && t.ExecutedAt >= yesterday)
            .Select(t => new 
            {
                High = SqlFunc.AggregateMax(t.Price),
                Low = SqlFunc.AggregateMin(t.Price),
                Volume = SqlFunc.AggregateSum(t.Quantity)
            })
            .FirstAsync();

        if (stats == null) return null;

        // Get Last Price & 24h ago Price separately (for change %)
        var lastPrice = await _db.Queryable<TradeHistory>()
            .Where(t => t.Symbol == symbol)
            .OrderBy(t => t.ExecutedAt, OrderByType.Desc)
            .Select(t => t.Price)
            .FirstAsync();

        var openPrice = await _db.Queryable<TradeHistory>()
            .Where(t => t.Symbol == symbol && t.ExecutedAt >= yesterday)
            .OrderBy(t => t.ExecutedAt, OrderByType.Asc)
            .Select(t => t.Price)
            .FirstAsync();

        decimal changePercent = 0;
        if (openPrice > 0)
        {
            changePercent = ((lastPrice - openPrice) / openPrice) * 100;
        }

        return new MarketStats(
            Symbol: symbol,
            LastPrice: lastPrice,
            High24h: stats.High,
            Low24h: stats.Low,
            Volume24h: stats.Volume,
            Change24hPercent: Math.Round(changePercent, 2)
        );
    }
}
/*
   * `MarketStats`：這是一個 DTO (Data Transfer Object) / Projection (投影模型)。
    * 特徵：它沒有對應到資料庫的任何一張表。它是我們從資料庫查詢結果「計算/拼湊」出來
        的一個臨時物件，專門用來傳遞給 UI 顯示。
*/
public record MarketStats(
    string Symbol,
    decimal LastPrice,
    decimal High24h,
    decimal Low24h,
    decimal Volume24h,
    decimal Change24hPercent);
