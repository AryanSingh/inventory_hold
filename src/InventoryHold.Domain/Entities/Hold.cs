using InventoryHold.Contracts;

namespace InventoryHold.Domain.Entities;

public class HoldItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class Hold
{
    public string Id { get; set; } = string.Empty;
    public string HoldId { get; set; } = string.Empty;
    public List<HoldItem> Items { get; set; } = new();
    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => Status == HoldStatus.Active;

    public void Release()
    {
        if (!IsActive)
            throw new InvalidOperationException($"Cannot release hold in {Status} status.");
        Status = HoldStatus.Released;
        ReleasedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (!IsActive)
            throw new InvalidOperationException($"Cannot expire hold in {Status} status.");
        Status = HoldStatus.Expired;
    }
}
