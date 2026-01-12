using OrderManagement.Infrastructure.Repositories;
using OrderManagement.Infrastructure.Repositories.Abstractions;
using System.Data;

namespace OrderManagement.Infrastructure.UnitOfWork.ActionScope;

/// <summary>
/// Unit of Work コンテキストの実装
/// </summary>
internal class UnitOfWorkContext(IDbConnection connection, IDbTransaction? transaction) : IUnitOfWorkContext
{
    /// <inheritdoc />
    public IOrderRepository Orders
        => field ??= new OrderRepository(connection, transaction);

    /// <inheritdoc />
    public IInventoryRepository Inventory
        => field ??= new InventoryRepository(connection, transaction);

    /// <inheritdoc />
    public IAuditLogRepository AuditLogs
        => field ??= new AuditLogRepository(connection, transaction);
}
