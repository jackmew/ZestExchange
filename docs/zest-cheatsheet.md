# why C#

# Why Distributed
所有的技術架構演變，歸根結底都在解決這三個物理資源的矛盾：
1. CPU 時間（算得快不快）
2. Memory 記憶體空間（裝得下多少當下資料）
3. 網路/IO 延遲（資料搬運要多久）

latency
packet loss

### CAP 定理
1. Consistency (一致性)
2. Availability (可用性 高可用)
3. Partition Tolerance (分區容錯性) 必選，因為網路一定有可能斷或延遲

P + C (不保證可用性) = 銀行轉帳   
P + A (不保證一致性) = Facebook按讚 

# why C# Aspire

# why Olreans

Actor Model

Single Thread Loop


## Addressable Location Transparency

# why postgresql
支持各種索引

mysql: B+樹index
1. 變種B+樹(mysql) -> B-Link樹
2. GIN(Generalized Inverted Index) 索引 詞元 -> 通用倒排索引 -> 平替 ElasticSearch
- 數字文本
3. GIN + JsonB(Json Binary). 二進制Json 去掉空格換行等 -> 二進制存儲 -> 平替MongoDB
- 文本|Json
4. GIST(Generalized Search Tree), 一維數據, Rectangle Tree 矩形術 R-Tree
- 地理位置
5. BRIN(Block Range Index) 關心範圍邊界 -> 時序數據 InfluxDB
- 時序數據

# why SqlSugar

# why k8s

# DDD