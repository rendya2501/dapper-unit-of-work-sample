using Microsoft.Data.Sqlite;
using OrderManagement.Infrastructure.Repositories.AuditLog;
using OrderManagement.Infrastructure.Repositories.Inventory;
using OrderManagement.Infrastructure.Repositories.Order;
using System.Data;

namespace OrderManagement.Infrastructure.UnitOfWork;

/// <summary>
/// Unit of Work の実装クラス
/// 設計ポイント：
/// - Repository は外部から注入せず、内部で生成する
/// - これにより Transaction の自動共有が保証される
/// - Repository 側は Transaction を意識する必要がない
/// - Commit/Rollback を async 化し、将来的な拡張性を確保
/// </summary>

public class UnitOfWork(string connectionString) : IUnitOfWork
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    // Repository インスタンス（遅延初期化）
    private IOrderRepository? _orderRepository;
    private IInventoryRepository? _inventoryRepository;
    private IAuditLogRepository? _auditLogRepository;

    // ------------------------------------------------------------------------
    // Repository 取得（重要：ここで Transaction を自動注入）
    // ------------------------------------------------------------------------

    /// <summary>
    /// OrderRepository 取得
    /// 設計意図：getter内で生成することで、確実に同一Transaction を共有
    /// </summary>
    public IOrderRepository Orders
    {
        get
        {
            EnsureConnection();
            return _orderRepository ??= new OrderRepository(_connection!, _transaction);
        }
    }

    public IInventoryRepository Inventory
    {
        get
        {
            EnsureConnection();
            return _inventoryRepository ??= new InventoryRepository(_connection!, _transaction);
        }
    }

    public IAuditLogRepository AuditLogs
    {
        get
        {
            EnsureConnection();
            return _auditLogRepository ??= new AuditLogRepository(_connection!, _transaction);
        }
    }


    // ------------------------------------------------------------------------
    // トランザクション制御
    // ------------------------------------------------------------------------

    /// <summary>
    /// トランザクション開始
    /// Connection が未作成の場合は自動的に生成する
    /// 
    /// 設計ノート：
    /// IDbConnection.BeginTransaction() は同期APIのため、ここは同期的に実装
    /// </summary>
    public void BeginTransaction()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction is already started.");
        }

        EnsureConnection();
        _transaction = _connection!.BeginTransaction();
    }

    /// <summary>
    /// トランザクションコミット（非同期版）
    /// 
    /// 設計ノート：
    /// 現在の IDbTransaction.Commit() は同期APIだが、
    /// 将来的に非同期DB操作やロギング処理を追加する可能性を考慮し async 化
    /// </summary>
    public void Commit()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Transaction is not started.");
        }

        try
        {
            // 現状は同期的だが、将来の拡張性のため async メソッドとして定義
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    /// <summary>
    /// トランザクションロールバック（非同期版）
    /// </summary>
    public  void Rollback( )
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Transaction is not started.");
        }

        try
        {
            _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }


    // ------------------------------------------------------------------------
    // 内部ヘルパー
    // ------------------------------------------------------------------------

    private void EnsureConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
        }
    }

    // ------------------------------------------------------------------------
    // Dispose パターン（同期・非同期両対応）
    // ------------------------------------------------------------------------

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _transaction?.Dispose();
            _connection?.Dispose();
        }

        _disposed = true;
    }
}
