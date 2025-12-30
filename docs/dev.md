# 加 FastEndpoints + Swagger
```
cd /Users/zest/Projects/c-net/ZestExchange/ZestExchange.ApiService 
dotnet add package FastEndpoints 
dotnet add package FastEndpoints.Swagger
```

## EndPoints

```
  FastEndpoints 官方推薦一個 endpoint 一個 class，但如果你偏好分組，可以用資料夾：

  Endpoints/
  ├── Orders/
  │   ├── PlaceOrderEndpoint.cs
  │   ├── CancelOrderEndpoint.cs
  │   └── GetOrderEndpoint.cs
  └── OrderBook/
      └── GetOrderBookEndpoint.cs
```