using Dapper;
using System.Data;

namespace OrderManagement.Infrastructure.Repositories.Order;

/// <summary>
/// 設計ポイント：
/// - Repository は Connection と Transaction を受け取るが、
///   Begin/Commit/Rollback は一切行わない
/// - これらは UnitOfWork が責任を持つ
/// - Repository は純粋にデータアクセスのみに専念
/// </summary>
public class OrderRepository(IDbConnection connection, IDbTransaction? transaction) : IOrderRepository
{
    public async Task<int> CreateAsync(Domain.Entities.Order order)
    {
        const string sql = """
            INSERT INTO Orders (ProductId, Quantity, CreatedAt)
            VALUES (@ProductId, @Quantity, @CreatedAt);
            SELECT last_insert_rowid();
            """;

        // Dapper に Transaction を渡す（UoW経由で注入されたもの）
        return await connection.ExecuteScalarAsync<int>(sql, order, transaction);
    }

    public async Task<Domain.Entities.Order?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Orders WHERE Id = @Id";
        return await connection.QueryFirstOrDefaultAsync<Domain.Entities.Order>(sql, new { Id = id }, transaction);
    }
}
