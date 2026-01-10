using Dapper;
using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.Repositories.Abstractions;
using System.Data;

namespace OrderManagement.Infrastructure.Repositories;

/// <summary>
/// 在庫リポジトリの実装
/// </summary>
/// <remarks>
/// <para><strong>設計原則</strong></para>
/// <list type="bullet">
/// <item>Repository は Connection と Transaction を受け取るが、Begin/Commit/Rollback は一切行わない</item>
/// <item>トランザクション管理は UnitOfWork が責任を持つ</item>
/// <item>Repository は純粋にデータアクセスのみに専念</item>
/// </list>
/// </remarks>
/// <param name="connection">データベース接続</param>
/// <param name="transaction">トランザクション（UnitOfWork から注入）</param>
public class InventoryRepository(IDbConnection connection, IDbTransaction? transaction)
    : IInventoryRepository
{
    /// <inheritdoc />
    public async Task<Inventory?> GetByProductIdAsync(int productId)
    {
        const string sql = "SELECT * FROM Inventory WHERE ProductId = @ProductId";
        return await connection.QueryFirstOrDefaultAsync<Inventory>(
            sql, new { ProductId = productId }, transaction);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Inventory>> GetAllAsync()
    {
        const string sql = "SELECT * FROM Inventory ORDER BY ProductId";
        return await connection.QueryAsync<Inventory>(sql, transaction: transaction);
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(Inventory inventory)
    {
        const string sql = """
            INSERT INTO Inventory (ProductName, Stock, UnitPrice)
            VALUES (@ProductName, @Stock, @UnitPrice);
            SELECT last_insert_rowid();
            """;
        return await connection.ExecuteScalarAsync<int>(sql, inventory, transaction);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(int productId, string productName, int stock, decimal unitPrice)
    {
        const string sql = """
            UPDATE Inventory 
            SET ProductName = @ProductName, Stock = @Stock, UnitPrice = @UnitPrice
            WHERE ProductId = @ProductId
            """;
        await connection.ExecuteAsync(sql,
            new { ProductId = productId, ProductName = productName, Stock = stock, UnitPrice = unitPrice },
            transaction);
    }

    /// <inheritdoc />
    public async Task<int> UpdateStockAsync(int productId, int newStock)
    {
        const string sql = "UPDATE Inventory SET Stock = @Stock WHERE ProductId = @ProductId";
        return await connection.ExecuteAsync(
            sql, new { ProductId = productId, Stock = newStock }, transaction);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int productId)
    {
        const string sql = "DELETE FROM Inventory WHERE ProductId = @ProductId";
        await connection.ExecuteAsync(sql, new { ProductId = productId }, transaction);
    }
}