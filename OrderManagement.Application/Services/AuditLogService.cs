using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.UnitOfWork;

namespace OrderManagement.Application.Services;

/// <summary>
/// 監査ログサービスの実装
/// </summary>
/// <param name="unitOfWorkFactory">UnitOfWork ファクトリ</param>
public class AuditLogService(Func<IUnitOfWork> unitOfWorkFactory) : IAuditLogService
{
    /// <inheritdoc />
    public async Task<IEnumerable<AuditLog>> GetAllAsync(int limit = 100)
    {
        using var uow = unitOfWorkFactory();
        return await uow.AuditLogs.GetAllAsync(limit);
    }
}
