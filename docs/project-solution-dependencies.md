
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

## Project & Solution
In C#/.NET terminology:
  - Each .csproj is called a Project
  - The .sln file groups multiple Projects into a Solution
  - So ZestExchange.ApiService is a "Project", not a "module"


## NuGet v.s Maven

About NuGet and alternatives:
  - NuGet still uses XML in .csproj (though it's much simpler than old packages.config)
  - There's no Gradle-like DSL alternative that's mainstream in .NET
  - However, .NET CLI commands like dotnet add package and dotnet add reference make it easier without editing XML directly
  - The modern SDK-style .csproj is much simpler than the old verbose format

  C# 術語

  | 層級          | C# 術語  | Java 對應                   |
  |---------------|----------|-----------------------------|
  | .sln          | Solution | Multi-module project (root) |
  | .csproj       | Project  | Module / Subproject         |
  | NuGet package | Package  | Artifact / Dependency       |

  所以正確說法是：
  - ZestExchange.ApiService 是一個 Project
  - ZestExchange.sln 包含多個 Projects，叫 Solution

  ---
  Gradle vs NuGet

  // Gradle - DSL 語法，簡潔
  dependencies {
      implementation project(':contracts')
      implementation 'com.google:guava:31.0'
  }

  <!-- NuGet - 還是 XML 😅 -->
  <ProjectReference Include="..\Contracts\Contracts.csproj" />
  <PackageReference Include="FastEndpoints" Version="7.1.1" />

  NuGet 沒有 Gradle 那樣的 DSL 改進，但有 CLI 指令可以不用手寫 XML：

  # 加外部套件
  dotnet add package FastEndpoints

  # 加專案引用
  dotnet add reference ../ZestExchange.Contracts

  這些指令會自動修改 .csproj，所以其實不太需要手動編輯 XML。

  ---
  總結：C# 叫 Project，不叫 module。NuGet 仍是 XML，但 CLI 指令讓你不用直接碰它。
