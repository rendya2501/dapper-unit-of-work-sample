using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Exceptions;
using OrderManagement.Infrastructure.UnitOfWork.ActionScope;

namespace OrderManagement.Application.Services;

/// <summary>
/// 在庫サービスの実装
/// </summary>
/// <remarks>
/// 在庫管理のビジネスロジックを実装します。
/// </remarks>
public class InventoryService(IUnitOfWork uow) : IInventoryService
{
    /// <inheritdoc />
    public async Task<IEnumerable<Inventory>> GetAllAsync()
    {
        return await uow.QueryAsync(async ctx => await ctx.Inventory.GetAllAsync());
    }

    /// <inheritdoc />
    public async Task<Inventory?> GetByProductIdAsync(int productId)
    {
        return await uow.QueryAsync(async ctx => await ctx.Inventory.GetByProductIdAsync(productId));
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(string productName, int stock, decimal unitPrice)
    {
        return await uow.CommandAsync(async ctx =>
        {
            var productId = await ctx.Inventory.CreateAsync(new Inventory
            {
                ProductName = productName,
                Stock = stock,
                UnitPrice = unitPrice
            });

            await ctx.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "INVENTORY_CREATED",
                Details = $"ProductId={productId}, Name={productName}, Stock={stock}, Price={unitPrice}",
                CreatedAt = DateTime.UtcNow
            });

            return productId;
        });
    }

    /// <inheritdoc />
    public async Task UpdateAsync(int productId, string productName, int stock, decimal unitPrice)
    {
        await uow.CommandAsync(async ctx =>
        {
            _ = await ctx.Inventory.GetByProductIdAsync(productId) // Ensure product exists before updating
                ?? throw new NotFoundException("Product", productId.ToString());

            await ctx.Inventory.UpdateAsync(productId, productName, stock, unitPrice);

            await ctx.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "INVENTORY_UPDATED",
                Details = $"ProductId={productId}, Name={productName}, Stock={stock}, Price={unitPrice}",
                CreatedAt = DateTime.UtcNow
            });
        });
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int productId)
    {
        await uow.CommandAsync(async ctx =>
        {
            var existing = await ctx.Inventory.GetByProductIdAsync(productId)
                ?? throw new NotFoundException("Product", productId.ToString());

            await ctx.Inventory.DeleteAsync(productId);

            await ctx.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "INVENTORY_DELETED",
                Details = $"ProductId={productId}, Name={existing.ProductName}",
                CreatedAt = DateTime.UtcNow
            });
        });
    }
}
