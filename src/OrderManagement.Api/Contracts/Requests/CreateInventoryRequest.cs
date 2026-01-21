namespace OrderManagement.Api.Contracts.Requests;

/// <summary>
/// 在庫作成リクエスト
/// </summary>
/// <param name="ProductName">商品名</param>
/// <param name="Stock">在庫数</param>
/// <param name="UnitPrice">単価</param>
public record CreateInventoryRequest(string ProductName, int Stock, decimal UnitPrice);
