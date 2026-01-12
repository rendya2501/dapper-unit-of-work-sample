using OrderManagement.Application.Models;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Infrastructure.UnitOfWork.ActionScope;

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
/// <param name="uow">Unit of Work（DI経由で注入）</param>
public class OrderService(IUnitOfWork uow) : IOrderService
{
    /// <inheritdoc />
    public async Task<int> CreateOrderAsync(int customerId, List<OrderItem> items)
    {
        return await uow.CommandAsync(async ctx =>
        {
            if (items.Count == 0)
                throw new BusinessRuleViolationException("Order must have at least one item.");

            // 1. 注文集約を構築
            var order = new Order
            {
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow
            };

            // 2. 各商品の在庫確認と注文明細追加
            foreach (var item in items)
            {
                var inventory = await ctx.Inventory.GetByProductIdAsync(item.ProductId)
                    ?? throw new NotFoundException("Product", item.ProductId.ToString());

                if (inventory.Stock < item.Quantity)
                {
                    throw new BusinessRuleViolationException(
                        $"Insufficient stock for {inventory.ProductName}. " +
                        $"Available: {inventory.Stock}, Requested: {item.Quantity}");
                }

                // 在庫減算
                await ctx.Inventory.UpdateStockAsync(
                    item.ProductId,
                    inventory.Stock - item.Quantity);

                // 注文明細を追加（集約ルートを通じて）
                order.AddDetail(item.ProductId, item.Quantity, inventory.UnitPrice);
            }

            // 3. 注文を永続化（明細も一緒に保存される）
            var orderId = await ctx.Orders.CreateAsync(order);

            // 4. 監査ログ記録
            await ctx.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "ORDER_CREATED",
                Details = $"OrderId={orderId}, CustomerId={customerId}, " +
                         $"Items={items.Count}, Total={order.TotalAmount:C}",
                CreatedAt = DateTime.UtcNow
            });

            return orderId;
        });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        return await uow.QueryAsync(async ctx => await ctx.Orders.GetAllAsync());
    }

    /// <inheritdoc />
    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        var order = await uow.QueryAsync(async ctx => await ctx.Orders.GetByIdAsync(id));

        return order ?? throw new NotFoundException("Order", id.ToString());
    }
}
