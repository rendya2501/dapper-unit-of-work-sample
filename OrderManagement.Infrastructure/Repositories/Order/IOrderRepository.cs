namespace OrderManagement.Infrastructure.Repositories.Order;

public interface IOrderRepository
{
    Task<int> CreateAsync(Domain.Entities.Order order);
    Task<Domain.Entities.Order?> GetByIdAsync(int id);
}
