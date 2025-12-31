# why C#

# why C# Aspire

# why Olreans

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