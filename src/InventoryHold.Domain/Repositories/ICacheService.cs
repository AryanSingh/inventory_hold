using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Repositories;

public interface ICacheService
{
    Task<List<InventoryItem>?> GetInventoryAsync();
    Task SetInventoryAsync(List<InventoryItem> items);
    Task InvalidateInventoryAsync();
}
