# Place Order
```
curl -X POST http://localhost:5327/api/orders \
-H "Content-Type: application/json" \
-d '{"symbol":"BTC-USDT","side":"Sell","type":"Limit","price":50000,"quantity":1}'
```

# Orderbook by symbol
```
curl http://localhost:5327/api/orderbook/BTC-USDT
{"symbol":"BTC-USDT","bids":[],"asks":[{"price":50000,"totalQuantity":1}],"timestamp":"2025-12-30T11:17:06.403924Z"}
```