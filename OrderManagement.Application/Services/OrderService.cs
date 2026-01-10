using OrderManagement.Application.Models;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Infrastructure.UnitOfWork;

namespace OrderManagement.Application.Services;

/// <summary>
/// 注文サービスの実装
/// </summary>
/// <remarks>
/// <para><strong>ビジネスロジックの実装場所</strong></para>
/// <list type="bullet">
/// <item>在庫確認・減算</item>
/// <item>注文集約の構築</item>
/// <item>トランザクション境界の管理</item>
/// <item>監査ログの記録</item>
/// </list>
/// </remarks>
/// <param name="unitOfWorkFactory">UnitOfWork ファクトリ</param>
public class OrderService(Func<IUnitOfWork> unitOfWorkFactory) : IOrderService
{
    /// <inheritdoc />
    public async Task<int> CreateOrderAsync(int customerId, List<OrderItem> items)
    {
        if (items.Count == 0)
            throw new BusinessRuleViolationException("Order must have at least one item.");

        // ===== トランザクション境界開始 =====
        using var uow = unitOfWorkFactory();
        uow.BeginTransaction();

        try
        {
            // 1. 注文集約を構築
            var order = new Order
            {
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow
            };

            // 2. 各商品の在庫確認と注文明細追加
            foreach (var item in items)
            {
                var inventory = await uow.Inventory.GetByProductIdAsync(item.ProductId)
                    ?? throw new NotFoundException("Product", item.ProductId);

                if (inventory.Stock < item.Quantity)
                {
                    throw new BusinessRuleViolationException(
                        $"Insufficient stock for {inventory.ProductName}. " +
                        $"Available: {inventory.Stock}, Requested: {item.Quantity}");
                }

                // 在庫減算
                await uow.Inventory.UpdateStockAsync(
                    item.ProductId,
                    inventory.Stock - item.Quantity);

                // 注文明細を追加（集約ルートを通じて）
                order.AddDetail(item.ProductId, item.Quantity, inventory.UnitPrice);
            }

            // 3. 注文を永続化（明細も一緒に保存される）
            var orderId = await uow.Orders.CreateAsync(order);

            // 4. 監査ログ記録
            await uow.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "ORDER_CREATED",
                Details = $"OrderId={orderId}, CustomerId={customerId}, " +
                         $"Items={items.Count}, Total={order.TotalAmount:C}",
                CreatedAt = DateTime.UtcNow
            });

            uow.Commit();
            return orderId;
        }
        catch
        {
            uow.Rollback();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        using var uow = unitOfWorkFactory();
        return await uow.Orders.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        using var uow = unitOfWorkFactory();

        var order = await uow.Orders.GetByIdAsync(id);

        return order ?? throw new NotFoundException("Order", id);
    }
}
