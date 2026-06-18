using InventoryHold.Contracts;
using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Repositories;

public interface IHoldRepository
{
    Task<Hold?> GetByHoldIdAsync(string holdId);
    Task<Hold> CreateAsync(Hold hold);
    Task<bool> ReleaseAsync(string holdId);
    Task<bool> MarkExpiredAsync(string holdId);
    Task<bool> TryMarkExpiredAsync(string holdId, HoldStatus expectedStatus);
    Task<List<Hold>> FindExpiredAsync(DateTime asOf);
    Task<List<Hold>> GetActiveHoldsAsync();
}
