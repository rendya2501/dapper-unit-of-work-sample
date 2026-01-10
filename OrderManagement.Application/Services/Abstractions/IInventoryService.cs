using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Services.Abstractions;

/// <summary>
/// 在庫サービスのインターフェース
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// すべての在庫を取得します
    /// </summary>
    Task<IEnumerable<Inventory>> GetAllAsync();

    /// <summary>
    /// 商品IDを指定して在庫を取得します
    /// </summary>
    Task<Inventory?> GetByProductIdAsync(int productId);

    /// <summary>
    /// 在庫を作成します
    /// </summary>
    Task<int> CreateAsync(string productName, int stock, decimal unitPrice);

    /// <summary>
    /// 在庫を更新します
    /// </summary>
    Task UpdateAsync(int productId, string productName, int stock, decimal unitPrice);

    /// <summary>
    /// 在庫を削除します
    /// </summary>
    Task DeleteAsync(int productId);
}
