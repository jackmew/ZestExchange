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

https://aspire.dev/

Orchestrate

Remove the complexity of infrastructure components - ELK, Prometheus, Grapana, Jaeger

# why Olreans

Actor Model

Single Thread Loop
強調 Orleans 預設是 Turn-based Concurrency (一次處理一個請求)，這保證了資料一致性。

## Addressable Location Transparency
## 第一層地獄：茫茫人海，他在哪？ (Addressing)
- Orleans 的解法 (Distributed Directory)： 它實作了一個分佈式目錄 (Distributed Hash Table 變體)。

## 第二層地獄：單一啟動的承諾 (Single Activation Guarantee)
- Orleans 的解法 (Cluster Membership Protocol)

## 第三層地獄：資源回收的藝術 (Activation GC)
- Orleans 的解法 (Activation Garbage Collection)： 這是 Orleans 內建的核心功能。 它會自動偵測哪些 Grain 閒置太久，自動呼叫 DeactivateAsync 把狀態存檔，然後從 RAM 清除。
## 第四層地獄：序列化的效能 (Serialization)
- Orleans 的解法 (Orleans Serializer)： 它內建了一個黑科技序列化引擎。

### 你引入 Orleans，不是為了買它的 Mailbox，你是為了買它的 「分佈式操作系統 (Distributed OS)」：
1. 自動導航 (不用管 Actor 在哪)。
2. 自動維穩 (不用管 Server 掛掉怎麼辦)。
3. 自動清理 (不用管記憶體夠不夠)。

# 一句話說Orleans

讓開發者像寫單機代碼一樣寫分佈式代碼，且不需要手動管理物件的創建與銷毀，問題就解決了。

# 拆解根本問題
1. 併發難題 (Concurrency): 多個人同時搶一張票，傳統做法需要鎖 (Lock)，容易死鎖或變慢。
2. 分佈式難題 (Distribution): 代碼跑在 A 服務器，但數據在 B 服務器，網絡通訊很麻煩。
3. 資源管理難題 (Lifecycle): 為了應付流量，我們開了太多物件 (Objects)，內存爆了；或者服務器掛了，物件丟了。

# Virtual Actor Model (虛擬Actor模型)
## 概念 A：Grains (粒子/穀粒)
- 魔法： 你不需要「聘請」王小明（New Object）。只要你心裡想著「我要找王小明」，他就已經存在了。
## 概念 B：Virtual Actor (虛擬角力者)
- 這意味著： 你可以擁有 10 億個用戶 Grain，但只需要一點點內存，因為只有「正在講話」的 Grain 才會佔用內存。
## 概念 C：Single Threaded (單線程執行)
- 如果同時有 10 個人打給他，Orleans 會自動讓這些電話排隊。王小明不需要擔心「同時被兩個人說話干擾」。這讓你寫代碼時完全不需要寫 lock，也不會發生數據衝突。
## 概念 D：Location Transparency (位置透明)
- 比喻： 你不需要知道王小明在哪個辦公室（哪台服務器）。你只要對著空氣喊「呼叫王小明」，Orleans 系統會自己找到他在哪，或者在哪裡復活他。


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