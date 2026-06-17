using Domain.Entities;

namespace Domain.Interfaces;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();
    Task<InventoryItem?> GetByProductIdAsync(string productId);
    Task<bool> DecrementAvailabilityAsync(string productId, int quantity);
    Task IncrementAvailabilityAsync(string productId, int quantity);
    Task SeedAsync(List<InventoryItem> items);
    Task<bool> IsEmptyAsync();
}
