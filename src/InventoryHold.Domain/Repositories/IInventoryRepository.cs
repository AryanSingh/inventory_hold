using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Repositories;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();
    Task<InventoryItem?> GetByProductIdAsync(string productId);
    Task<bool> DecrementAvailabilityAsync(string productId, int quantity);
    Task IncrementAvailabilityAsync(string productId, int quantity);
    Task UpsertManyAsync(List<InventoryItem> items);
    Task<bool> IsEmptyAsync();
}
