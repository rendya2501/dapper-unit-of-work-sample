using Dapper;
using System.Data;

namespace OrderManagement.Infrastructure.Repositories.AuditLog;

public class AuditLogRepository(IDbConnection connection, IDbTransaction? transaction) : IAuditLogRepository
{
    public async Task CreateAsync(Domain.Entities.AuditLog log)
    {
        const string sql = """
            INSERT INTO AuditLog (Action, Details, CreatedAt)
            VALUES (@Action, @Details, @CreatedAt)          
            """;

        await connection.ExecuteAsync(sql, log, transaction);
    }
}
