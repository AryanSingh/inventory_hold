namespace InventoryHold.Domain.Repositories;

public interface IEventPublisher
{
    Task PublishHoldCreatedAsync(string holdId, List<Contracts.HoldItemDto> items, DateTime createdAt, DateTime expiresAt);
    Task PublishHoldReleasedAsync(string holdId, List<Contracts.HoldItemDto> items, DateTime releasedAt);
    Task PublishHoldExpiredAsync(string holdId, List<Contracts.HoldItemDto> items, DateTime expiredAt);
}
