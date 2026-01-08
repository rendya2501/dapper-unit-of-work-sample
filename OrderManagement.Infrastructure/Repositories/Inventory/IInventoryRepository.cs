namespace OrderManagement.Infrastructure.Repositories.Inventory;

public interface IInventoryRepository
{
    Task<Domain.Entities.Inventory?> GetByProductIdAsync(int productId);
    Task UpdateStockAsync(int productId, int newStock);
}
