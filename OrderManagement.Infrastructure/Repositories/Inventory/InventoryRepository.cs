using Dapper;
using System.Data;

namespace OrderManagement.Infrastructure.Repositories.Inventory;

public class InventoryRepository(IDbConnection connection, IDbTransaction? transaction) : IInventoryRepository
{
    public async Task<Domain.Entities.Inventory?> GetByProductIdAsync(int productId)
    {
        const string sql = "SELECT * FROM Inventory WHERE ProductId = @ProductId";
        return await connection.QueryFirstOrDefaultAsync<Domain.Entities.Inventory>(
            sql, new { ProductId = productId }, transaction);
    }

    public async Task UpdateStockAsync(int productId, int newStock)
    {
        const string sql = "UPDATE Inventory SET Stock = @Stock WHERE ProductId = @ProductId";
        await connection.ExecuteAsync(sql, new { ProductId = productId, Stock = newStock }, transaction);
    }
}
