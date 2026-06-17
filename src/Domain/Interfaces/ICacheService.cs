using Domain.Entities;

namespace Domain.Interfaces;

public interface ICacheService
{
    Task<List<InventoryItem>?> GetInventoryAsync();
    Task SetInventoryAsync(List<InventoryItem> items);
    Task InvalidateInventoryAsync();
}
