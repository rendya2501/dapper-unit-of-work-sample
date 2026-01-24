# Dapper Unit of Work Sample

**レガシー環境でDapperを使った安全なトランザクション管理の実装サンプル**

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Dapper](https://img.shields.io/badge/Dapper-2.1-orange)](https://github.com/DapperLib/Dapper)
[![SQLite](https://img.shields.io/badge/SQLite-3-green)](https://www.sqlite.org/)

---

## 🎯 このプロジェクトについて

Dapperを使用した**古典的なUnit of Workパターン**の実装サンプルです。

### なぜ「古典的」な実装なのか

このプロジェクトは、あえて**泥臭く、愚直な**実装を採用しています。

```csharp
// この実装スタイル
await uow.BeginTransactionAsync();
try
{
    await orderRepo.CreateAsync(order);
    await inventoryRepo.UpdateStock(...);
    await uow.CommitAsync();
}
catch
{
    await uow.RollbackAsync();
    throw;
}
```

理由は以下の通りです。

1. **レガシー環境への導入が容易**
   - Result型やActionScopeパターンは既存コードベースに馴染まない
   - try-catch構造は既存のエラーハンドリングと親和性が高い
   - チームメンバー全員が理解できる

2. **Unit of Workの本質を学べる**
   - トランザクション境界が明示的で分かりやすい
   - Begin → 処理 → Commit/Rollbackの流れが追いやすい
   - デバッグ時に何が起きているか把握しやすい

3. **現実的な選択肢**
   - 中小規模のプロジェクトでは十分実用的
   - 過度に抽象化せず、保守しやすい
   - トランザクション制御の責任が明確

### ✅ メリット

| 項目 | 説明 |
|------|------|
| **明確なトランザクション境界** | BeginTransaction、Commit、Rollbackで境界が一目瞭然 |
| **シンプルで理解しやすい** | 特殊な知識不要、C#の基本文法だけで理解できる |
| **デバッグしやすい** | トランザクションの開始・終了をログやブレークポイントで追える |
| **レガシーコードとの統合** | 既存のtry-catch構造にそのまま組み込める |
| **学習教材として最適** | Unit of Workパターンの本質を理解できる |

### ⚠️ デメリット

| 項目 | 説明 |
|------|------|
| **Rollback忘れのリスク** | catchブロックでRollbackを忘れるとデータ不整合 |
| **try-catchが必須** | 毎回同じパターンを書く必要がある |
| **コード量が多い** | Result型ベースのパターンより冗長 |

### 他の実装パターンとの比較

**ActionScope/Result型パターン（モダンな実装）**
```csharp
// より洗練されているが、学習コストが高い
var result = await uow.ExecuteAsync(async () =>
{
    await orderRepo.CreateAsync(order);
    await inventoryRepo.UpdateStock(...);
    return orderId;
});
```

**このプロジェクトの実装（古典的・明示的）**
```csharp
// 泥臭いが、何が起きているか明確
await uow.BeginTransactionAsync();
try
{
    await orderRepo.CreateAsync(order);
    await inventoryRepo.UpdateStock(...);
    await uow.CommitAsync();
}
catch
{
    await uow.RollbackAsync();
    throw;
}
```

### 解決する課題

Dapperには組み込みのトランザクション管理がないため、以下の問題が発生しがちです。

```csharp
// ❌ よくある問題
public async Task CreateOrderAsync(Order order)
{
    // トランザクションの渡し忘れ
    await orderRepo.CreateAsync(order);  // Transaction: null
    await inventoryRepo.UpdateStock(...); // Transaction: null
    // → 片方だけ成功してデータ不整合
}
```

このプロジェクトでは、明示的なUnit of Workパターンでこれらの問題を構造的に防止します。

---

## 🚀 クイックスタート

### 前提条件

- .NET 10.0 SDK以上
- 任意のIDE（Visual Studio / Rider / VS Code）

### 1. リポジトリをクローン

```bash
git clone https://github.com/rendya2501/Dapper.UnitOfWork.Sample.git
cd Dapper.UnitOfWork.Sample
```

### 2. プロジェクトを実行

```bash
cd src/OrderManagement.Api
dotnet run
```

### 3. APIを試す

ブラウザで http://localhost:5076/scalar/v1 を開く

**または**

```bash
# 注文を作成
curl -X POST http://localhost:5076/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "items": [
      { "productId": 1, "quantity": 2 }
    ]
  }'
```

---

## 📖 基本的な使い方

### DI登録（Program.cs）

```csharp
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

// 接続を登録
builder.Services.AddScoped<IDbConnection>(sp => 
    new SqliteConnection(connectionString));

// DbSessionを登録（接続とトランザクションを管理）
builder.Services.AddScoped<DbSession>();
builder.Services.AddScoped<IDbSessionManager>(sp => 
    sp.GetRequiredService<DbSession>());
builder.Services.AddScoped<IDbSession>(sp => 
    sp.GetRequiredService<DbSession>());

// Unit of Workを登録
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Repositoryを登録
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Serviceを登録
builder.Services.AddScoped<IOrderService, OrderService>();
```

### Service層での使用

#### パターン1：読み取り専用操作

```csharp
public class OrderService
{
    private readonly IOrderRepository _orderRepo;

    public OrderService(IOrderRepository orderRepo)
    {
        _orderRepo = orderRepo;
    }

    // 読み取りのみ → トランザクション不要
    public async Task<Order?> GetOrderAsync(int orderId)
    {
        return await _orderRepo.GetByIdAsync(orderId);
    }
}
```

#### パターン2：単一Repository操作（書き込み）

```csharp
public class InventoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IAuditLogRepository _auditLogRepo;

    public async Task<int> CreateAsync(
        string productName, int stock, decimal unitPrice)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            // 在庫作成
            var productId = await _inventoryRepo.CreateAsync(new Inventory
            {
                ProductName = productName,
                Stock = stock,
                UnitPrice = unitPrice
            });

            // 監査ログ記録
            await _auditLogRepo.CreateAsync(new AuditLog
            {
                Action = "INVENTORY_CREATED",
                Details = $"ProductId={productId}",
                CreatedAt = DateTime.UtcNow
            });

            await _uow.CommitAsync();
            return productId;
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }
}
```

#### パターン3：複数Repository横断操作

```csharp
public class OrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IOrderRepository _orderRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IAuditLogRepository _auditLogRepo;

    public async Task<int> CreateOrderAsync(
        int customerId, List<OrderItem> items)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            // 1. 在庫確認と減算
            foreach (var item in items)
            {
                var inventory = await _inventoryRepo
                    .GetByProductIdAsync(item.ProductId)
                    ?? throw new NotFoundException("Product", 
                        item.ProductId.ToString());

                if (inventory.Stock < item.Quantity)
                    throw new BusinessRuleException("Insufficient stock");

                await _inventoryRepo.UpdateStockAsync(
                    item.ProductId, 
                    inventory.Stock - item.Quantity);
            }

            // 2. 注文作成
            var order = new Order
            {
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow
            };
            
            foreach (var item in items)
            {
                var inventory = await _inventoryRepo
                    .GetByProductIdAsync(item.ProductId);
                order.AddDetail(
                    item.ProductId, 
                    item.Quantity, 
                    inventory!.UnitPrice);
            }

            var orderId = await _orderRepo.CreateAsync(order);

            // 3. 監査ログ記録
            await _auditLogRepo.CreateAsync(new AuditLog
            {
                Action = "ORDER_CREATED",
                Details = $"OrderId={orderId}, Total={order.TotalAmount:C}",
                CreatedAt = DateTime.UtcNow
            });

            // 4. すべて成功 → コミット
            await _uow.CommitAsync();
            return orderId;
        }
        catch
        {
            // エラー発生 → ロールバック
            await _uow.RollbackAsync();
            throw;
        }
    }
}
```

---

## 💡 よくあるパターン

### 早期returnがあるトランザクション処理

```csharp
public async Task<ProcessResult> ProcessOrderAsync(OrderRequest request)
{
    await _uow.BeginTransactionAsync();
    try
    {
        var inventory = await _inventoryRepo
            .GetByProductIdAsync(request.ProductId);
        
        // 在庫不足の場合は早期return
        if (inventory.Stock < request.Quantity)
        {
            await _uow.RollbackAsync();  // 明示的にロールバック
            return new ProcessResult 
            { 
                Success = false, 
                Message = "在庫不足" 
            };
        }

        // 通常処理
        await _inventoryRepo.UpdateStockAsync(
            request.ProductId, 
            inventory.Stock - request.Quantity);
            
        var orderId = await _orderRepo.CreateAsync(order);

        await _uow.CommitAsync();
        return new ProcessResult { Success = true, OrderId = orderId };
    }
    catch
    {
        await _uow.RollbackAsync();
        throw;
    }
}
```

### 長時間処理との組み合わせ

```csharp
public async Task ProcessOrderWithNotificationAsync(OrderRequest request)
{
    int orderId;

    // トランザクション内では最小限の処理のみ
    await _uow.BeginTransactionAsync();
    try
    {
        orderId = await _orderRepo.CreateAsync(order);
        await _uow.CommitAsync();
    }
    catch
    {
        await _uow.RollbackAsync();
        throw;
    }

    // トランザクション外で長時間処理
    await _emailService.SendOrderConfirmationAsync(orderId);
    await _smsService.SendNotificationAsync(orderId);
}
```

### バッチ処理

```csharp
public async Task ProcessBatchOrdersAsync(List<OrderRequest> requests)
{
    await _uow.BeginTransactionAsync();
    try
    {
        foreach (var request in requests)
        {
            var inventory = await _inventoryRepo
                .GetByProductIdAsync(request.ProductId);
            
            if (inventory.Stock >= request.Quantity)
            {
                await _inventoryRepo.UpdateStockAsync(
                    request.ProductId,
                    inventory.Stock - request.Quantity);
                    
                await _orderRepo.CreateAsync(new Order { /* ... */ });
            }
        }

        // バッチ全体を1トランザクションでコミット
        await _uow.CommitAsync();
    }
    catch
    {
        await _uow.RollbackAsync();
        throw;
    }
}
```

---

## ✅ ベストプラクティス

### 1. トランザクションは最小限に保つ

```csharp
// ✅ 良い例：DB操作のみトランザクション内
var orderId = await CreateOrderInTransaction(request);
await SendNotificationOutsideTransaction(orderId);

// ❌ 悪い例：外部API呼び出しまでトランザクション内
await _uow.BeginTransactionAsync();
try
{
    var orderId = await _orderRepo.CreateAsync(order);
    await _externalApi.CallAsync();  // トランザクションが長時間ロック
    await _uow.CommitAsync();
}
catch { await _uow.RollbackAsync(); throw; }
```

### 2. 早期returnでは必ずRollback

```csharp
await _uow.BeginTransactionAsync();
try
{
    if (invalidCondition)
    {
        await _uow.RollbackAsync();  // 必ず明示
        return;
    }
    
    // 処理続行
    await _uow.CommitAsync();
}
catch
{
    await _uow.RollbackAsync();
    throw;
}
```

### 3. Repositoryは純粋にデータアクセスのみ

```csharp
// ✅ Repository: トランザクション管理は一切しない
public class OrderRepository : IOrderRepository
{
    private readonly IDbSession _session;

    public async Task<int> CreateAsync(Order order)
    {
        return await _session.Connection.ExecuteScalarAsync<int>(
            sql, order, _session.Transaction);  // Transactionを渡すだけ
    }
}

// ✅ Service: トランザクション管理とビジネスロジック
public class OrderService
{
    public async Task<int> CreateOrderAsync(...)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            // ビジネスロジック + Repository呼び出し
            await _uow.CommitAsync();
        }
        catch { await _uow.RollbackAsync(); throw; }
    }
}
```

### 4. 読み取り専用操作ではトランザクション不要

```csharp
// ✅ 良い例
public async Task<Order?> GetOrderAsync(int id)
{
    return await _orderRepo.GetByIdAsync(id);
}

// ❌ 悪い例
public async Task<Order?> GetOrderAsync(int id)
{
    await _uow.BeginTransactionAsync();
    try
    {
        var order = await _orderRepo.GetByIdAsync(id);
        await _uow.CommitAsync();
        return order;
    }
    catch
    {
        await _uow.RollbackAsync();
        throw;
    }
}
```

---

## 🏗️ プロジェクト構成

```
Dapper.UnitOfWork.Sample/
│
├── OrderManagement.Api/              # Web API層
│   ├── Controllers/                  # エンドポイント
│   ├── Middleware/                   # エラーハンドリング
│   └── Program.cs                    # DI設定・起動
│
├── OrderManagement.Application/      # ビジネスロジック層
│   ├── Services/                     # 複数Repository横断処理
│   ├── Repositories/                 # Repository インターフェース
│   └── Common/
│       ├── IUnitOfWork.cs           # トランザクション管理
│       ├── IDbSession.cs            # 接続・トランザクション参照
│       └── IDbSessionManager.cs     # トランザクション設定
│
├── OrderManagement.Domain/           # ドメイン層
│   ├── Entities/                     # エンティティ
│   └── Exceptions/                   # ドメイン例外
│
└── OrderManagement.Infrastructure/   # データアクセス層
    └── Persistence/
        ├── UnitOfWork.cs            # トランザクション実装
        ├── DbSession.cs             # セッション管理
        ├── Repositories/            # Repository実装
        └── Database/
            └── DatabaseInitializer.cs
```

---

## 🔍 トラブルシューティング

### Q1. トランザクションがコミットされない

**原因**: 例外が発生してRollbackされている

**解決策**: ログを確認し、例外の原因を特定

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "OrderManagement": "Debug"
    }
  }
}
```

### Q2. デッドロックが発生する

**原因**: トランザクション内で長時間処理を実行している

**解決策**: 外部APIコールや重い処理はトランザクション外へ

```csharp
// ✅ 良い例
await _uow.BeginTransactionAsync();
try
{
    var orderId = await _orderRepo.CreateAsync(order);
    await _uow.CommitAsync();
}
catch { await _uow.RollbackAsync(); throw; }

// トランザクション外で実行
await _externalApi.NotifyAsync(orderId);
```

### Q3. 早期returnでデータが残る

**原因**: Rollbackを忘れている

**解決策**: 早期returnの前に必ずRollback

```csharp
await _uow.BeginTransactionAsync();
try
{
    if (invalidCondition)
    {
        await _uow.RollbackAsync();  // これを忘れない
        return;
    }
    await _uow.CommitAsync();
}
catch { await _uow.RollbackAsync(); throw; }
```

---

## 📚 詳細情報

### 設計ドキュメント

- [DESIGN.md](./DESIGN.md) - 設計思想と使い方の詳細

### 参考資料

- [Martin Fowler - Unit of Work](https://martinfowler.com/eaaCatalog/unitOfWork.html)
- [Microsoft - Repository Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [Dapper Documentation](https://github.com/DapperLib/Dapper)

---

## 🧪 テストの実行

```bash
cd Tests
dotnet test
```

---

## 📝 ライセンス

MIT License
