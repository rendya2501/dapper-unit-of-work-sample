namespace OrderManagement.Domain.Entities;

/// <summary>
/// 注文エンティティ（集約ルート）
/// </summary>
/// <remarks>
/// <para><strong>集約ルート (Aggregate Root) の責務</strong></para>
/// <list type="bullet">
/// <item>Order は OrderDetail の親エンティティであり、集約の境界を定義</item>
/// <item>OrderDetail は Order を通じてのみ操作される</item>
/// <item>Repository は集約ルート（Order）に対してのみ存在する</item>
/// <item>OrderDetail 単独での永続化は許可されない</item>
/// </list>
/// 
/// <para><strong>不変条件の維持</strong></para>
/// <para>
/// 注文明細の追加・削除は Order を通じて行われることで、
/// 注文全体の整合性（合計金額、数量チェックなど）が保証される。
/// </para>
/// </remarks>
public class Order
{
    /// <summary>
    /// 注文ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 顧客ID
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 注文日時
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 注文明細のコレクション
    /// </summary>
    /// <remarks>
    /// 注文明細は Order を通じてのみアクセスされる。
    /// 外部から直接操作されないよう、setter は private に設定。
    /// </remarks>
    public List<OrderDetail> Details { get; private set; } = [];

    /// <summary>
    /// 注文合計金額
    /// </summary>
    /// <remarks>
    /// 注文明細から自動計算される値。
    /// DB には永続化せず、取得時に計算する。
    /// </remarks>
    public decimal TotalAmount => Details.Sum(d => d.UnitPrice * d.Quantity);

    /// <summary>
    /// 注文明細を追加します
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="quantity">数量</param>
    /// <param name="unitPrice">単価</param>
    /// <remarks>
    /// ドメインロジック（ビジネスルール）をエンティティ内で実装。
    /// 例: 数量は1以上でなければならない。
    /// </remarks>
    /// <exception cref="ArgumentException">数量が1未満の場合</exception>
    public void AddDetail(int productId, int quantity, decimal unitPrice)
    {
        if (quantity < 1)
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));

        Details.Add(new OrderDetail
        {
            OrderId = Id,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        });
    }
}