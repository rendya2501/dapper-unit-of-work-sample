using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.UnitOfWork;

namespace OrderManagement.Application.Services;

public class OrderService(Func<IUnitOfWork> unitOfWorkFactory) : IOrderService
{
    /// <summary>
    /// 注文作成処理（本番シナリオ）
    /// 1トランザクションで以下を実行：
    /// - 在庫確認
    /// - 在庫減算
    /// - 注文登録
    /// - 監査ログ記録
    /// </summary>
    public async Task<int> CreateOrderAsync(int productId, int quantity)
    {
        // ===== トランザクション境界開始 =====
        using var uow = unitOfWorkFactory();
        uow.BeginTransaction();

        try
        {
            // 1. 在庫確認（InventoryRepository 使用）
            var inventory = await uow.Inventory.GetByProductIdAsync(productId) 
                ?? throw new InvalidOperationException($"Product {productId} not found.");

            if (inventory.Stock < quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock. Available: {inventory.Stock}, Requested: {quantity}");
            }

            // 2. 在庫減算（InventoryRepository 使用）
            await uow.Inventory.UpdateStockAsync(productId, inventory.Stock - quantity);

            // 3. 注文登録（OrderRepository 使用）
            var order = new Order
            {
                ProductId = productId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            };
            var orderId = await uow.Orders.CreateAsync(order);

            // 4. 監査ログ記録（AuditLogRepository 使用）
            var auditLog = new AuditLog
            {
                Action = "ORDER_CREATED",
                Details = $"OrderId={orderId}, ProductId={productId}, Quantity={quantity}",
                CreatedAt = DateTime.UtcNow
            };

            await uow.AuditLogs.CreateAsync(auditLog);

            // すべて成功 → Commit（非同期化）
            uow.Commit();
            // ===== トランザクション境界終了（成功） =====

            return orderId;
        }
        catch
        {
            // 例外発生 → Rollback（非同期化）
            uow.Rollback();
            // ===== トランザクション境界終了（失敗） =====
            throw;
        }
    }
}
