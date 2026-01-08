
```cs
// ============================================================================
// プロジェクト構成
// ============================================================================
//
// OrderManagement.sln
// │
// ├─ OrderManagement.Api/                    # Web API プロジェクト
// │  ├─ Controllers/
// │  │  └─ OrdersController.cs              # エンドポイント定義
// │  ├─ Program.cs                           # アプリケーション起動 + DI設定
// │  └─ OrderManagement.Api.csproj
// │
// ├─ OrderManagement.Application/            # ビジネスロジック層
// │  ├─ Services/
// │  │  ├─ IOrderService.cs
// │  │  └─ OrderService.cs                  # 複数Repository横断処理
// │  └─ OrderManagement.Application.csproj
// │
// ├─ OrderManagement.Domain/                 # ドメインモデル
// │  ├─ Entities/
// │  │  ├─ Order.cs
// │  │  ├─ Inventory.cs
// │  │  └─ AuditLog.cs
// │  └─ OrderManagement.Domain.csproj
// │
// └─ OrderManagement.Infrastructure/         # データアクセス層
//    ├─ UnitOfWork/
//    │  ├─ IUnitOfWork.cs
//    │  └─ UnitOfWork.cs                    # トランザクション管理
//    ├─ Repositories/
//    │  ├─ IOrderRepository.cs
//    │  ├─ OrderRepository.cs
//    │  ├─ IInventoryRepository.cs
//    │  ├─ InventoryRepository.cs
//    │  ├─ IAuditLogRepository.cs
//    │  └─ AuditLogRepository.cs
//    ├─ Database/
//    │  └─ DatabaseInitializer.cs           # スキーマ初期化
//    └─ OrderManagement.Infrastructure.csproj
//
// ============================================================================
// ソリューション名の意図
// ============================================================================
//
// 推奨: OrderManagement.sln
//
// 理由:
// - "Management" は CRUD + ビジネスロジックを含む実務的な名称
// - "Order" は複数Repositoryを横断する典型的なドメイン
// - レガシー環境でも違和感なく導入できる保守的なネーミング
//
// 避けるべき名称:
// ✗ DapperUnitOfWorkSample.sln → 技術詳細が表に出すぎ
// ✗ CleanArchitectureSample.sln → アーキテクチャパターンを強調しすぎ
// ✗ ECommercePlatform.sln → スコープが大きすぎる
//
// ============================================================================
// 設計意図：
// Dapperには組み込みのトランザクション管理機構が存在しないため、
// 複数Repositoryを跨ぐ処理において「Transaction渡し忘れ」が発生しやすい。
// 本実装では UnitOfWork が Repository を生成・保持することで、
// 構造的にトランザクションの共有を強制し、本番環境での事故を防ぐ。
// ============================================================================

using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.AspNetCore.Mvc;

// ============================================================================
// 1. Unit of Work インターフェース定義
// ============================================================================

/// <summary>
/// Unit of Work パターンの中核インターフェース
/// - Connection/Transaction の生成・管理を担当
/// - Repository の取得メソッドを提供
/// - トランザクション境界を明確化
/// 
/// 設計ノート：
/// - BeginTransaction は同期的（IDbConnection.BeginTransaction が同期API）
/// - Commit/Rollback は async 化（将来的な非同期DB対応を考慮）
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    // トランザクション制御
    void BeginTransaction();
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    
    // Repository 取得（UoWが生成・管理する）
    IOrderRepository Orders { get; }
    IInventoryRepository Inventory { get; }
    IAuditLogRepository AuditLogs { get; }
}

// ============================================================================
// 2. Unit of Work 実装
// ============================================================================

/// <summary>
/// Unit of Work の実装クラス
/// 設計ポイント：
/// - Repository は外部から注入せず、内部で生成する
/// - これにより Transaction の自動共有が保証される
/// - Repository 側は Transaction を意識する必要がない
/// - Commit/Rollback を async 化し、将来的な拡張性を確保
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly string _connectionString;
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    
    // Repository インスタンス（遅延初期化）
    private IOrderRepository? _orderRepository;
    private IInventoryRepository? _inventoryRepository;
    private IAuditLogRepository? _auditLogRepository;
    
    private bool _disposed;

    public UnitOfWork(string connectionString)
    {
        _connectionString = connectionString;
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
            throw new InvalidOperationException("Transaction is already started.");

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
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction is not started.");

        try
        {
            // 現状は同期的だが、将来の拡張性のため async メソッドとして定義
            await Task.Run(() => _transaction.Commit(), cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// トランザクションロールバック（非同期版）
    /// </summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction is not started.");

        try
        {
            await Task.Run(() => _transaction.Rollback(), cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

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
    // 内部ヘルパー
    // ------------------------------------------------------------------------
    
    private void EnsureConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
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

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
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

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_transaction != null)
            await _transaction.DisposeAsync();

        if (_connection != null)
            await _connection.DisposeAsync();
    }
}

// ============================================================================
// 3. Entity 定義
// ============================================================================

public class Order
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Inventory
{
    public int ProductId { get; set; }
    public int Stock { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// 4. Repository インターフェース定義
// ============================================================================

public interface IOrderRepository
{
    Task<int> CreateAsync(Order order);
    Task<Order?> GetByIdAsync(int id);
}

public interface IInventoryRepository
{
    Task<Inventory?> GetByProductIdAsync(int productId);
    Task UpdateStockAsync(int productId, int newStock);
}

public interface IAuditLogRepository
{
    Task CreateAsync(AuditLog log);
}

// ============================================================================
// 5. Repository 実装
// ============================================================================

/// <summary>
/// 設計ポイント：
/// - Repository は Connection と Transaction を受け取るが、
///   Begin/Commit/Rollback は一切行わない
/// - これらは UnitOfWork が責任を持つ
/// - Repository は純粋にデータアクセスのみに専念
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction? _transaction;

    // UnitOfWork から Connection/Transaction を受け取る
    public OrderRepository(IDbConnection connection, IDbTransaction? transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<int> CreateAsync(Order order)
    {
        const string sql = @"
            INSERT INTO Orders (ProductId, Quantity, CreatedAt)
            VALUES (@ProductId, @Quantity, @CreatedAt);
            SELECT last_insert_rowid();";

        // Dapper に Transaction を渡す（UoW経由で注入されたもの）
        return await _connection.ExecuteScalarAsync<int>(sql, order, _transaction);
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Orders WHERE Id = @Id";
        return await _connection.QueryFirstOrDefaultAsync<Order>(sql, new { Id = id }, _transaction);
    }
}

public class InventoryRepository : IInventoryRepository
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction? _transaction;

    public InventoryRepository(IDbConnection connection, IDbTransaction? transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<Inventory?> GetByProductIdAsync(int productId)
    {
        const string sql = "SELECT * FROM Inventory WHERE ProductId = @ProductId";
        return await _connection.QueryFirstOrDefaultAsync<Inventory>(
            sql, new { ProductId = productId }, _transaction);
    }

    public async Task UpdateStockAsync(int productId, int newStock)
    {
        const string sql = "UPDATE Inventory SET Stock = @Stock WHERE ProductId = @ProductId";
        await _connection.ExecuteAsync(sql, new { ProductId = productId, Stock = newStock }, _transaction);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction? _transaction;

    public AuditLogRepository(IDbConnection connection, IDbTransaction? transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task CreateAsync(AuditLog log)
    {
        const string sql = @"
            INSERT INTO AuditLog (Action, Details, CreatedAt)
            VALUES (@Action, @Details, @CreatedAt)";
        
        await _connection.ExecuteAsync(sql, log, _transaction);
    }
}

// ============================================================================
// 6. Service 層実装
// ============================================================================

/// <summary>
/// OrderService：ビジネスロジックを実装
/// 設計ポイント：
/// - Repository に直接依存せず、UnitOfWork のみに依存
/// - トランザクション境界を Service 層で明確に定義
/// - 複数 Repository を跨ぐ処理を1トランザクションで実行
/// </summary>
public interface IOrderService
{
    Task<int> CreateOrderAsync(int productId, int quantity);
}

public class OrderService : IOrderService
{
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    // UnitOfWork は毎回生成するため、Factory パターンを使用
    public OrderService(Func<IUnitOfWork> unitOfWorkFactory)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
    }

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
        await using var uow = _unitOfWorkFactory();
        uow.BeginTransaction();

        try
        {
            // 1. 在庫確認（InventoryRepository 使用）
            var inventory = await uow.Inventory.GetByProductIdAsync(productId);
            if (inventory == null)
                throw new InvalidOperationException($"Product {productId} not found.");

            if (inventory.Stock < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock. Available: {inventory.Stock}, Requested: {quantity}");

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
            await uow.CommitAsync();
            // ===== トランザクション境界終了（成功） =====

            return orderId;
        }
        catch
        {
            // 例外発生 → Rollback（非同期化）
            await uow.RollbackAsync();
            // ===== トランザクション境界終了（失敗） =====
            throw;
        }
    }
}

// ============================================================================
// 7. Controller 実装
// ============================================================================

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var orderId = await _orderService.CreateOrderAsync(request.ProductId, request.Quantity);
            return Ok(new { OrderId = orderId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error", Details = ex.Message });
        }
    }
}

public record CreateOrderRequest(int ProductId, int Quantity);

// ============================================================================
// 8. DI 登録 (Program.cs) + appsettings.json 対応
// ============================================================================

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ===== appsettings.json から接続文字列を取得 =====
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // DB初期化
        InitializeDatabase(connectionString);

        // DI登録
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // UnitOfWork は毎回新しいインスタンスを生成
        // 接続文字列をクロージャでキャプチャ
        builder.Services.AddScoped<Func<IUnitOfWork>>(sp => 
            () => new UnitOfWork(connectionString));

        builder.Services.AddScoped<IOrderService, OrderService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();
        app.Run();
    }

    // ============================================================================
    // 9. データベース初期化（SQLite スキーマ）
    // ============================================================================

    private static void InitializeDatabase(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // テーブル作成
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            )");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Inventory (
                ProductId INTEGER PRIMARY KEY,
                Stock INTEGER NOT NULL
            )");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Action TEXT NOT NULL,
                Details TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )");

        // サンプルデータ投入
        var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Inventory");
        if (count == 0)
        {
            connection.Execute(@"
                INSERT INTO Inventory (ProductId, Stock) VALUES
                (1, 100),
                (2, 50),
                (3, 200)");
        }

        Console.WriteLine("Database initialized successfully.");
    }
}

// ============================================================================
// 設計の総括
// ============================================================================
//
// 【なぜこの設計を採用したのか】
//
// 1. Dapperのトランザクション管理の課題
//    - Dapperは単なるマッパーであり、トランザクション管理機構を持たない
//    - Repository が個別に Transaction を受け取る設計では、
//      「渡し忘れ」「異なるTransactionの使用」といったヒューマンエラーが発生
//
// 2. 構造的な安全性の確保
//    - UnitOfWork が Repository を生成・保持することで、
//      すべての Repository に自動的に同一 Transaction が共有される
//    - Repository 側は Transaction の存在を意識するだけでよく、
//      管理責任を負わない
//
// 3. 本番運用での現実性
//    - レガシー環境に段階的に導入可能
//    - 過度な抽象化を避け、コードの見通しを維持
//    - CQRS/MediatR等の高度なパターンは不要
//
// 4. 責務の明確化
//    - UnitOfWork: トランザクション境界の管理
//    - Repository: データアクセスロジック
//    - Service: ビジネスロジック（複数Repository の組み合わせ）
//    - Controller: HTTPリクエスト/レスポンスの処理
//
// 【事故を防ぐ構造】
// - Repository は Transaction を外部から受け取らない（受け取れない）
// - Service は Repository に直接依存しない（依存できない）
// - トランザクション制御は Service 層でのみ記述される
//
// 【非同期化のポイント】
// - Commit/Rollback を async 化し、将来的な拡張性を確保
// - IAsyncDisposable を実装し、await using パターンに対応
// - BeginTransaction は IDbConnection の制約により同期的
//
// 【設定管理】
// - 接続文字列は appsettings.json で管理
// - 環境ごとの設定切り替えが容易
// - 本番/開発環境で異なる設定を使用可能
//
// この設計により、コンパイル時・実行時の両面で
// トランザクション管理ミスを構造的に防止します。
// ============================================================================

// ============================================================================
// appsettings.json サンプル
// ============================================================================
/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=sample.db"
  }
}
*/

// ============================================================================
// appsettings.Development.json サンプル（開発環境用）
// ============================================================================
/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=sample_dev.db"
  }
}
*/

// ============================================================================
// appsettings.Production.json サンプル（本番環境用）
// ============================================================================
/*
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/data/production.db"
  }
}
*/
```
