namespace OrderManagement.Api.Requests;

/// <summary>
/// 注文作成リクエスト
/// </summary>
/// <remarks>
/// <para><strong>Validation ルール</strong></para>
/// <list type="bullet">
/// <item>CustomerId: 1以上の整数</item>
/// <item>Items: 1件以上必須</item>
/// <item>各アイテム: ProductId は1以上、Quantity は1以上</item>
/// </list>
/// </remarks>
/// <param name="CustomerId">顧客ID</param>
/// <param name="Items">注文アイテムのリスト</param>
public record CreateOrderRequest(int CustomerId, List<OrderItemRequest> Items);
