namespace InventoryHold.Contracts;

public enum HoldStatus
{
    Active,
    Released,
    Expired
}

public record HoldItemDto
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public record HoldDto
{
    public string HoldId { get; init; } = string.Empty;
    public List<HoldItemDto> Items { get; init; } = new();
    public HoldStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? ReleasedAt { get; init; }
}
