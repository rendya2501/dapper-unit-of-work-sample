using OrderManagement.Infrastructure.Repositories;
using OrderManagement.Infrastructure.Repositories.Abstractions;
using System.Data;

namespace OrderManagement.Infrastructure.UnitOfWork.Basic;

/// <summary>
/// Unit of Work の実装クラス
/// </summary>
/// <remarks>
/// <para><strong>設計原則</strong></para>
/// <list type="number">
/// <item>Repository は外部から注入せず、内部で生成する</item>
/// <item>すべての Repository に同一の Transaction が自動的に共有される</item>
/// <item>Connection は BeginTransaction 時に1度だけ確立される</item>
/// </list>
/// 
/// <para><strong>遅延初期化 (Lazy Initialization) とは</strong></para>
/// <para>
/// Repository インスタンスは、getter で初めてアクセスされた時点で生成される。
/// 使用されない Repository は生成されないため、メモリ効率が良い。
/// </para>
/// <code>
/// // Orders にアクセスした時点で初めて OrderRepository が生成される
/// var order = await uow.Orders.GetByIdAsync(1);
/// 
/// // Inventory を使わなければ InventoryRepository は生成されない
/// </code>
/// </remarks>
/// <remarks>
/// コンストラクタ（Connection を即座に生成・Open）
/// </remarks>
/// <remarks>
/// <para><strong>なぜコンストラクタで Connection を生成するのか</strong></para>
/// <list type="bullet">
/// <item>読み取り専用操作でも使えるようにするため</item>
/// <item>Repository getter で毎回 null チェックする必要がなくなる</item>
/// <item>コードがシンプルになる</item>
/// <item>接続プーリングにより、パフォーマンス問題は発生しない</item>
/// </list>
/// </remarks>
public class UnitOfWork(IDbConnection connection) : IUnitOfWork
{
    private IDbTransaction? _transaction;
    private bool _disposed;


    #region リポジトリ
    /// <inheritdoc />
    public IOrderRepository Orders
    {
        get => field ??= new OrderRepository(connection, _transaction);
        private set;
    }

    /// <inheritdoc />
    public IInventoryRepository Inventory
    {
        get => field ??= new InventoryRepository(connection, _transaction);
        private set;
    }

    /// <inheritdoc />
    public IAuditLogRepository AuditLogs
    {
        get => field ??= new AuditLogRepository(connection, _transaction);
        private set;
    }

    #endregion


    #region トランザクション制御
    /// <inheritdoc />
    public void BeginTransaction()
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction is already started.");

        _transaction = connection.BeginTransaction();

        // 既存の Repository インスタンスを破棄（新しい Transaction を使わせるため）
        ResetRepositories();
    }

    /// <inheritdoc />
    public void Commit()
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction is not started.");

        try
        {
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    /// <inheritdoc />
    public void Rollback()
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction is not started.");

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
    #endregion


    #region ヘルパー
    /// <summary>
    /// すべての Repository をリセットします
    /// </summary>
    /// <remarks>
    /// <para><strong>なぜリセットが必要か</strong></para>
    /// <para>
    /// BeginTransaction() を呼ぶと新しい Transaction が生成されるが、
    /// 既存の Repository インスタンスは古い Transaction を保持している。
    /// リセットすることで、次回アクセス時に新しい Transaction が注入される。
    /// </para>
    /// </remarks>
    private void ResetRepositories()
    {
        Orders = null!;
        Inventory = null!;
        AuditLogs = null!;

        // Repository を追加する場合は、ここに追加するだけでOK
        // 例: Products = null!;
    }
    #endregion


    #region Dispose パターン
    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソースを解放します
    /// </summary>
    /// <param name="disposing">マネージドリソースを解放する場合は true</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Transaction のみ Dispose（Connection は DI が管理）
            _transaction?.Dispose();
        }

        _disposed = true;
    }
    #endregion
}
