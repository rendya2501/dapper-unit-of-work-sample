namespace OrderManagement.Api.Contracts.Responses;

/// <summary>
/// 在庫作成レスポンス
/// </summary>
/// <param name="ProductId">商品ID</param>
public record CreateInventoryResponse(int ProductId);
