using SqlSugar;
using ZestExchange.Contracts.Orders;

namespace ZestExchange.Repository.Entities;

/*
   * `TradeHistory`：這是一個 Entity (實體)。
     屬性，直接對應到資料庫的一張表。它是資料庫結構的鏡像。
*/
[SugarTable("trade_history")]
public class TradeHistory
{
    [SugarColumn(IsPrimaryKey = true)] // No auto-increment, using Guid
    public Guid Id { get; set; }

    [SugarColumn(ColumnName = "symbol", Length = 20, IsNullable = false)]
    public string Symbol { get; set; } = null!;

    [SugarColumn(ColumnName = "price", ColumnDataType = "decimal(18,8)", IsNullable = false)]
    public decimal Price { get; set; }

    [SugarColumn(ColumnName = "quantity", ColumnDataType = "decimal(18,8)", IsNullable = false)]
    public decimal Quantity { get; set; }

    [SugarColumn(ColumnName = "taker_side", IsNullable = false)]
    public OrderSide TakerSide { get; set; }

    [SugarColumn(ColumnName = "maker_order_id", IsNullable = false)]
    public Guid MakerOrderId { get; set; }

    [SugarColumn(ColumnName = "taker_order_id", IsNullable = false)]
    public Guid TakerOrderId { get; set; }

    [SugarColumn(ColumnName = "executed_at", IsNullable = false)]
    public DateTime ExecutedAt { get; set; }
}
