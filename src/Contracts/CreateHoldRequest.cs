namespace Contracts;

public record CreateHoldRequest
{
    public string? HoldId { get; init; }
    public List<CreateHoldItemRequest> Items { get; init; } = new();
    public int? DurationMinutes { get; init; }
}

public record CreateHoldItemRequest
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
