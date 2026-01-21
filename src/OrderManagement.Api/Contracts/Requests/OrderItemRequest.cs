namespace OrderManagement.Api.Contracts.Requests;

/// <summary>
/// 注文アイテムリクエスト
/// </summary>
/// <param name="ProductId">商品ID</param>
/// <param name="Quantity">数量</param>
public record OrderItemRequest(int ProductId, int Quantity);
