using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using ZestExchange.Repository.Entities;

namespace ZestExchange.Repository;

/*
  這是一個 Layered Architecture (分層架構) 的經典案例：

   1. 定義 (`Extensions.cs`):
       * 我們在這個檔案裡寫了 services.AddScoped<ISqlSugarClient>(...)。
       * 這段程式碼定義了「如何建立一個資料庫連線物件」。它還沒被執行，只是先「註冊」在
         DI 容器的菜單上。

   2. 呼叫 (`Silo/Program.cs` & `Web/Program.cs`):
       * 這兩個應用程式都呼叫了 builder.Services.AddZestRepository(...)。
       * 這一行代碼等於是告訴 DI 容器：「請把剛才那份關於 SqlSugar 和 TradeRepository
         的菜單加入我的系統裡」。
       * 從這一刻起，Silo 和 Web 都具備了產生 TradeRepository 的能力。

   3. 使用 (`TradeRepository` 建構子):
       * 當 Web 需要顯示 OrderBook 時，它向 DI 容器要一個 TradeRepository。
       * DI 容器發現 TradeRepository 需要 ISqlSugarClient。
       * DI 容器根據第 1 步的定義，建立一個 SqlSugarClient，注入進去。

  為什麼 Silo 和 Web 都要呼叫 `AddZestRepository`？
  因為這兩個是完全獨立的應用程式 (Process)。
   * Silo: 需要 Repository 來 寫入 (Insert) 交易紀錄。
   * Web: 需要 Repository 來 讀取 (Select) 市場統計。

  它們不共享記憶體，所以必須各自設定自己的 DI
  容器。這就是為什麼同樣的設定要在兩邊都寫一次（透過 AddZestRepository
  這個共用方法來簡化）。

*/
public static class Extensions
{
    public static void AddZestRepository(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Register SqlSugarClient as Scoped (or Singleton if thread-safe config used)
        services.AddScoped<ISqlSugarClient>(s =>
        {
            var connectionString = configuration.GetConnectionString("exchangedb");
            
            SqlSugarClient db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            // Debug Log
            // db.Aop.OnLogExecuting = (sql, pars) => 
            // {
            //    Console.WriteLine(sql + "\r\n" + db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value)));
            //    Console.WriteLine(); 
            // };

            return db;
        });

        // 2. Register Repositories
        services.AddScoped<TradeRepository>();
    }
}
