namespace OrderManagement.Api.Contracts.Responses;

/// <summary>
/// 注文作成レスポンス
/// </summary>
/// <param name="OrderId">作成された注文のID</param>
public record CreateOrderResponse(int OrderId);
