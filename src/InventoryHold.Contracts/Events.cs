namespace InventoryHold.Contracts;

public record HoldCreatedEvent
{
    public string HoldId { get; init; } = string.Empty;
    public List<HoldItemDto> Items { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public record HoldReleasedEvent
{
    public string HoldId { get; init; } = string.Empty;
    public List<HoldItemDto> Items { get; init; } = new();
    public DateTime ReleasedAt { get; init; }
}

public record HoldExpiredEvent
{
    public string HoldId { get; init; } = string.Empty;
    public List<HoldItemDto> Items { get; init; } = new();
    public DateTime ExpiredAt { get; init; }
}

public record ReleaseHoldResponse
{
    public string Message { get; init; } = string.Empty;
    public string HoldId { get; init; } = string.Empty;
}
