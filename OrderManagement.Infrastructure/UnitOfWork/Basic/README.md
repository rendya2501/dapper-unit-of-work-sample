# Basic Unit of Work パターン

## 目次

- [概要と対象者](#概要と対象者)
- [メリット・デメリット](#メリットデメリット)
- [基本的な使い方](#基本的な使い方)
- [実装の詳細](#実装の詳細)
- [DI 登録](#di-登録programcs)
- [よくあるパターン](#よくあるパターン)
- [ベストプラクティス](#ベストプラクティス)
- [トラブルシューティング](#トラブルシューティング)
- [いつ使うべきか](#いつ使うべきか)
- [まとめ](#まとめ)

---

## 概要と対象者

### 概要

最もシンプルな Unit of Work 実装です。  
トランザクションを手動で制御し、Begin/Commit/Rollback を明示的に呼び出します。

### 対象者

- 🎓 Unit of Work パターンを初めて学ぶ人
- 📚 トランザクションの基礎を理解したい人
- 🔧 シンプルな実装を求める人
- 👥 小規模なプロジェクトを開発する人

---

## メリット・デメリット

### ✅ メリット

| 項目 | 説明 |
|------|------|
| **明確なトランザクション境界** | `BeginTransaction()`、`Commit()`、`Rollback()` で境界が明確 |
| **シンプルな実装** | コードが短く、理解しやすい |
| **デバッグしやすい** | トランザクションの開始・終了が追いやすい |
| **学習教材として最適** | トランザクションの仕組みを学べる |
| **レガシーコードとの統合** | 既存のコードに導入しやすい |
| **Read-only は軽量** | トランザクションなしで実行可能 |

### ⚠️ デメリット

| 項目 | 説明 |
|------|------|
| **Rollback 忘れリスク** | `catch` ブロックで `Rollback()` を忘れるとデータ不整合 |
| **try-catch が必須** | 例外ハンドリングのコードが煩雑 |
| **コード量が多い** | 同じパターンを何度も書く必要がある |

---

## 基本的な使い方

### 1. インターフェース

```csharp
public interface IUnitOfWork : IDisposable
{
    // Repository へのアクセス
    IOrderRepository Orders { get; }
    IInventoryRepository Inventory { get; }
    IAuditLogRepository AuditLogs { get; }
    
    // トランザクション制御
    void BeginTransaction();
    void Commit();
    void Rollback();
}
```

### 2. Write 操作（トランザクションあり）

```csharp
public class OrderService
{
    private readonly IUnitOfWork _uow;

    public OrderService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<int> CreateOrderAsync(int productId, int quantity)
    {
        // ★ トランザクション開始
        _uow.BeginTransaction();

        try
        {
            // 在庫確認
            var inventory = await _uow.Inventory.GetByProductIdAsync(productId);
            if (inventory == null)
                throw new InvalidOperationException("Product not found");

            if (inventory.Stock < quantity)
                throw new InvalidOperationException("Insufficient stock");

            // 在庫減算
            await _uow.Inventory.UpdateStockAsync(productId, inventory.Stock - quantity);

            // 注文作成
            var order = new Order
            {
                ProductId = productId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            };
            var orderId = await _uow.Orders.CreateAsync(order);

            // 監査ログ
            await _uow.AuditLogs.CreateAsync(new AuditLog
            {
                Action = "ORDER_CREATED",
                Details = $"OrderId={orderId}",
                CreatedAt = DateTime.UtcNow
            });

            // ★ 成功 → Commit
            _uow.Commit();

            return orderId;
        }
        catch
        {
            // ★ 失敗 → Rollback
            _uow.Rollback();
            throw;
        }
    }
}
```

### 3. Read 操作（トランザクションなし）

```csharp
public async Task<Order?> GetOrderAsync(int id)
{
    // ★ BeginTransaction を呼ばない（トランザクションなし）
    return await _uow.Orders.GetByIdAsync(id);
}
```

**ポイント：**
- Read-only 操作ではトランザクション不要
- Repository に `null` を渡すことでトランザクションなしで実行
- パフォーマンスが良い

---

## 実装の詳細

### Repository の実装

Repository は `IDbConnection` と `IDbTransaction?`（nullable）を受け取ります。

```csharp
public class OrderRepository : IOrderRepository
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction? _transaction; // ★ nullable

    public OrderRepository(IDbConnection connection, IDbTransaction? transaction)
    {
        _connection = connection;
        _transaction = transaction; // ★ null の場合もある
    }

    // Read 操作（トランザクションなしでもOK）
    public async Task<Order?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Orders WHERE Id = @Id";
        
        // ★ Transaction が null でもDapperは動作する
        return await _connection.QueryFirstOrDefaultAsync<Order>(
            sql, 
            new { Id = id }, 
            _transaction); // ← null の場合はトランザクションなし
    }

    // Write 操作（トランザクション必須）
    public async Task<int> CreateAsync(Order order)
    {
        const string sql = @"
            INSERT INTO Orders (ProductId, Quantity, CreatedAt)
            VALUES (@ProductId, @Quantity, @CreatedAt);
            SELECT last_insert_rowid();";
        
        // ★ Transaction を渡す（BeginTransaction 済みの場合）
        return await _connection.ExecuteScalarAsync<int>(
            sql, 
            order, 
            _transaction);
    }
}
```

**重要なポイント：**
- `IDbTransaction?` を nullable にする
- Dapper は Transaction が `null` の場合、トランザクションなしで実行
- Read-only では `null` を渡せば軽量に動作

### UnitOfWork の実装（抜粋）

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;

    public IOrderRepository Orders
        => _orders ??= new OrderRepository(_connection, _transaction);
        //                                                ^^^^^^^^^^^
        // ★ BeginTransaction 前は null が渡される

    public void BeginTransaction()
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction already started");

        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction not started");

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        if (_transaction == null)
            throw new InvalidOperationException("Transaction not started");

        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }
}
```

---

## DI 登録（Program.cs）

```csharp
using Microsoft.Data.Sqlite;
using OrderManagement.Infrastructure.UnitOfWork.Basic;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// 接続文字列
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found");

// IDbConnection を Scoped で登録
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

// Unit of Work を Scoped で登録
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

---

## よくあるパターン

### パターン1：Read-only 操作（推奨）

```csharp
public async Task<Order?> GetOrderAsync(int id)
{
    // ★ BeginTransaction を呼ばない
    var order = await _uow.Orders.GetByIdAsync(id);
    return order;
}
```

**メリット：**
- ✅ トランザクションなし（軽量）
- ✅ ロックなし
- ✅ パフォーマンスが良い

### パターン2：トランザクション中の Read

```csharp
public async Task<int> CreateOrderAsync(int productId, int quantity)
{
    _uow.BeginTransaction();

    try
    {
        // ★ この Read はトランザクション内
        var inventory = await _uow.Inventory.GetByProductIdAsync(productId);
        
        // Write 操作
        await _uow.Inventory.UpdateStockAsync(productId, inventory.Stock - quantity);
        
        // ★ 自分が書き込んだデータを確認（同じトランザクション内）
        var updated = await _uow.Inventory.GetByProductIdAsync(productId);
        
        _uow.Commit();
        return orderId;
    }
    catch
    {
        _uow.Rollback();
        throw;
    }
}
```

**ポイント：**
- BeginTransaction 後の Read は同じトランザクション内
- 自分が書き込んだデータが見える
- Isolation Level に依存しない

### パターン3：複数の独立したトランザクション

```csharp
public async Task ProcessMultipleOrdersAsync(List<OrderRequest> requests)
{
    foreach (var request in requests)
    {
        // ★ 各注文を独立したトランザクションで処理
        _uow.BeginTransaction();

        try
        {
            var order = new Order 
            { 
                ProductId = request.ProductId, 
                Quantity = request.Quantity 
            };
            await _uow.Orders.CreateAsync(order);
            
            _uow.Commit();
        }
        catch (Exception ex)
        {
            _uow.Rollback();
            // 1件失敗しても他は継続
            _logger.LogError(ex, "Failed to process order");
        }
    }
}
```

---

## ベストプラクティス

### ✅ 推奨

```csharp
// 1. Read-only ではトランザクションを張らない
public async Task<Order?> GetOrderAsync(int id)
{
    return await _uow.Orders.GetByIdAsync(id); // BeginTransaction なし
}

// 2. Write では必ず try-catch
_uow.BeginTransaction();
try
{
    // 処理
    _uow.Commit();
}
catch
{
    _uow.Rollback();
    throw; // ★ 例外は必ず再スロー
}

// 3. Rollback の後は必ず throw
catch
{
    _uow.Rollback();
    throw; // ← これを忘れない
}

// 4. トランザクションは Service 層で管理
public class OrderService
{
    public async Task CreateOrderAsync(...)
    {
        _uow.BeginTransaction(); // ← Service が責任を持つ
        try { ... }
    }
}
```

### ❌ アンチパターン

```csharp
// ❌ Read-only でトランザクションを張る
public async Task<Order?> GetOrderAsync(int id)
{
    _uow.BeginTransaction(); // 不要
    try
    {
        var order = await _uow.Orders.GetByIdAsync(id);
        _uow.Commit(); // 不要
        return order;
    }
    catch
    {
        _uow.Rollback();
        throw;
    }
}

// ❌ Rollback を忘れる
_uow.BeginTransaction();
try
{
    // 処理
    _uow.Commit();
}
catch
{
    // Rollback がない！
    throw;
}

// ❌ 例外を再スローしない
catch
{
    _uow.Rollback();
    return -1; // ← エラーが隠蔽される
}

// ❌ Repository がトランザクションを管理
public class OrderRepository
{
    public async Task CreateAsync(Order order)
    {
        _uow.BeginTransaction(); // ← Repository の責務ではない
        try { ... }
    }
}

// ❌ finally で Commit
finally
{
    _uow.Commit(); // ← 例外が発生しても Commit される
}
```

---

## トラブルシューティング

### Q1. Commit を忘れたらどうなる？

**A.** データは保存されません（Rollback される）。

```csharp
_uow.BeginTransaction();
try
{
    await _uow.Orders.CreateAsync(order);
    // Commit を忘れた
}
catch
{
    _uow.Rollback();
}
// → Dispose で自動 Rollback されるため、データは保存されない
```

### Q2. Rollback を忘れたらどうなる？

**A.** リソースリークやデッドロックの原因になります。

```csharp
_uow.BeginTransaction();
try
{
    await _uow.Orders.CreateAsync(order);
    _uow.Commit();
}
catch
{
    // Rollback を忘れた
    throw;
}
// → Transaction がそのまま残り、リソースリーク
```

**対策：** 必ず `catch` ブロックで `Rollback()` を呼ぶ

### Q3. 二重 Commit/Rollback は？

**A.** 例外が発生します。

```csharp
_uow.Commit();
_uow.Commit(); // → InvalidOperationException
```

### Q4. Read-only でトランザクションを張るべき？

**A.** 張る必要はありません。

```csharp
// ✅ 推奨：トランザクションなし
public async Task<Order?> GetOrderAsync(int id)
{
    return await _uow.Orders.GetByIdAsync(id);
}

// ❌ 非推奨：トランザクションあり
public async Task<Order?> GetOrderAsync(int id)
{
    _uow.BeginTransaction();
    try
    {
        var order = await _uow.Orders.GetByIdAsync(id);
        _uow.Commit();
        return order;
    }
    catch
    {
        _uow.Rollback();
        throw;
    }
}
```

**理由：**
- トランザクションは不要なオーバーヘッド
- SQLite ではロックが発生する可能性
- パフォーマンスが悪化

---

## いつ使うべきか

### ✅ 適している場合

- 🎓 学習目的
- 📝 小規模プロジェクト（数千行程度）
- 🔧 レガシーコードとの統合
- 👥 チームがトランザクションを理解していない

### ❌ 適していない場合

- 🏢 中規模以上の実務開発 → **Action Scope パターンを推奨**
- 📦 Outbox パターンを使う → **Action Scope パターンを推奨**
- 🔄 複数トランザクションを頻繁に扱う → **Action Scope パターンを推奨**
- 🚀 Clean Architecture / DDD → **Action Scope パターンを推奨**

---

## まとめ

**Basic Unit of Work パターンは：**

✅ 学習教材として最適  
✅ シンプルで理解しやすい  
✅ トランザクションの基礎が学べる  
✅ Read-only は軽量（トランザクションなし）  

⚠️ Rollback 忘れのリスクがある  
⚠️ 実務では Action Scope パターンを推奨  

---

**関連ドキュメント：**
- [Action Scope パターン](../ActionScope/README.md)
- [パターン比較](../../README.md)