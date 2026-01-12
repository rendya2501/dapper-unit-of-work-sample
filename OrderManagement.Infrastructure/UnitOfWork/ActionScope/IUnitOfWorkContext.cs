using OrderManagement.Infrastructure.Repositories.Abstractions;

namespace OrderManagement.Infrastructure.UnitOfWork.ActionScope;

/// <summary>
/// Unit of Work コンテキスト（Repository へのアクセスを提供）
/// </summary>
public interface IUnitOfWorkContext
{
    /// <summary>
    /// 注文リポジトリを取得します
    /// </summary>
    IOrderRepository Orders { get; }

    /// <summary>
    /// 在庫リポジトリを取得します
    /// </summary>
    IInventoryRepository Inventory { get; }

    /// <summary>
    /// 監査ログリポジトリを取得します
    /// </summary>
    IAuditLogRepository AuditLogs { get; }
}
