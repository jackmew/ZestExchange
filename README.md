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