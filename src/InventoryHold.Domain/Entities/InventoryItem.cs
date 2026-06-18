namespace InventoryHold.Domain.Entities;

public class InventoryItem
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool HasSufficientStock(int quantity) => AvailableQuantity >= quantity;
}
