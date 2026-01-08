using OrderManagement.Infrastructure.Repositories.AuditLog;
using OrderManagement.Infrastructure.Repositories.Inventory;
using OrderManagement.Infrastructure.Repositories.Order;

namespace OrderManagement.Infrastructure.UnitOfWork;

/// <summary>
/// Unit of Work パターンの中核インターフェース
/// </summary>
/// <remarks>
/// - Connection/Transaction の生成・管理を担当
/// - Repository の取得メソッドを提供
/// - トランザクション境界を明確化
/// 
/// 設計ノート：
/// - BeginTransaction は同期的（IDbConnection.BeginTransaction が同期API）
/// - Commit/Rollback は async 化（将来的な非同期DB対応を考慮）
/// </remarks>
public interface IUnitOfWork : IDisposable
{
    // トランザクション制御
    void BeginTransaction();
    void Commit();
    void Rollback();

    // Repository 取得（UoWが生成・管理する）
    IOrderRepository Orders { get; }
    IInventoryRepository Inventory { get; }
    IAuditLogRepository AuditLogs { get; }
}
