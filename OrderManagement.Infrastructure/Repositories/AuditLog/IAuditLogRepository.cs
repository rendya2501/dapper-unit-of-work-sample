namespace OrderManagement.Infrastructure.Repositories.AuditLog;

public interface IAuditLogRepository
{
    Task CreateAsync(Domain.Entities.AuditLog log);
}
