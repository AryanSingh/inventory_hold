namespace InventoryHold.Contracts;

public record InventoryItemDto
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int AvailableQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int TotalQuantity { get; init; }
    public DateTime UpdatedAt { get; init; }
}
