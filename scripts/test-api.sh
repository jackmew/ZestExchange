#!/bin/bash

# ZestExchange API 測試腳本
#
# 使用方式:
#   1. 先啟動: dotnet run --project ZestExchange.AppHost
#   2. 查看 Aspire Dashboard 找到 apiservice 的 port
#   3. 執行: ./test-api.sh [port]
#      例如: ./test-api.sh 5327
#
# 或者設定環境變數:
#   API_PORT=5327 ./test-api.sh

PORT=${1:-${API_PORT:-5327}}
API_BASE="http://localhost:$PORT"

echo "使用 API: $API_BASE"
echo "(如果 port 不對，請執行: ./test-api.sh <正確的port>)"
echo ""
SYMBOL="BTC-USDT"

echo "=========================================="
echo "ZestExchange API 測試"
echo "=========================================="
echo ""

# 1. 測試 OrderBook (初始應為空)
echo "1️⃣  取得初始 OrderBook..."
echo "GET /api/orderbook/$SYMBOL"
curl -s "$API_BASE/api/orderbook/$SYMBOL" | jq .
echo ""

# 2. 掛賣單
echo "2️⃣  掛賣單: Sell 1.0 @ 50000"
echo "POST /api/orders"
SELL_RESPONSE=$(curl -s -X POST "$API_BASE/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "'"$SYMBOL"'",
    "side": "Sell",
    "type": "Limit",
    "price": 50000,
    "quantity": 1.0
  }')
echo "$SELL_RESPONSE" | jq .
SELL_ORDER_ID=$(echo "$SELL_RESPONSE" | jq -r '.orderId')
echo "賣單 ID: $SELL_ORDER_ID"
echo ""

# 3. 再掛一個賣單
echo "3️⃣  掛賣單: Sell 0.5 @ 50100"
curl -s -X POST "$API_BASE/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "'"$SYMBOL"'",
    "side": "Sell",
    "type": "Limit",
    "price": 50100,
    "quantity": 0.5
  }' | jq .
echo ""

# 4. 查看 OrderBook (應該有 2 個 asks)
echo "4️⃣  查看 OrderBook (應有 2 個 Asks)..."
curl -s "$API_BASE/api/orderbook/$SYMBOL" | jq .
echo ""

# 5. 掛買單 (會撮合！)
echo "5️⃣  掛買單: Buy 0.5 @ 50000 (應該撮合!)"
curl -s -X POST "$API_BASE/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "'"$SYMBOL"'",
    "side": "Buy",
    "type": "Limit",
    "price": 50000,
    "quantity": 0.5
  }' | jq .
echo ""

# 6. 查看 OrderBook (賣單應該減少)
echo "6️⃣  查看 OrderBook (Sell@50000 應剩 0.5)..."
curl -s "$API_BASE/api/orderbook/$SYMBOL" | jq .
echo ""

# 7. 取消訂單
echo "7️⃣  取消訂單: $SELL_ORDER_ID"
echo "DELETE /api/orders/$SYMBOL/$SELL_ORDER_ID"
curl -s -X DELETE "$API_BASE/api/orders/$SYMBOL/$SELL_ORDER_ID" | jq .
echo ""

# 8. 最終 OrderBook
echo "8️⃣  最終 OrderBook..."
curl -s "$API_BASE/api/orderbook/$SYMBOL" | jq .
echo ""

echo "=========================================="
echo "測試完成！"
echo "=========================================="
