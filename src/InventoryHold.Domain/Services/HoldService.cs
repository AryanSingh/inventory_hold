using InventoryHold.Contracts;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;

namespace InventoryHold.Domain.Services;

public class HoldService
{
    private readonly IHoldRepository _holdRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICacheService _cacheService;
    private readonly int _defaultDurationMinutes;

    public HoldService(
        IHoldRepository holdRepository,
        IInventoryRepository inventoryRepository,
        IEventPublisher eventPublisher,
        ICacheService cacheService,
        int defaultDurationMinutes = 15)
    {
        _holdRepository = holdRepository;
        _inventoryRepository = inventoryRepository;
        _eventPublisher = eventPublisher;
        _cacheService = cacheService;
        _defaultDurationMinutes = defaultDurationMinutes;
    }

    public async Task<HoldDto> CreateHoldAsync(CreateHoldRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("At least one item is required.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity must be greater than 0 for product {item.ProductId}.");
        }

        var holdId = string.IsNullOrEmpty(request.HoldId)
            ? Guid.NewGuid().ToString()
            : request.HoldId;

        // Idempotency: Check if hold already exists
        var existingHold = await _holdRepository.GetByHoldIdAsync(holdId);
        if (existingHold != null)
        {
            return new HoldDto
            {
                HoldId = existingHold.HoldId,
                Items = existingHold.Items.Select(hi => new HoldItemDto
                {
                    ProductId = hi.ProductId,
                    ProductName = hi.ProductName,
                    Quantity = hi.Quantity
                }).ToList(),
                Status = existingHold.Status,
                CreatedAt = existingHold.CreatedAt,
                ExpiresAt = existingHold.ExpiresAt,
                ReleasedAt = existingHold.ReleasedAt
            };
        }

        var durationMinutes = request.DurationMinutes ?? _defaultDurationMinutes;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(durationMinutes);

        // Track which items were successfully decremented for rollback
        var decrementedItems = new List<(string ProductId, int Quantity)>();

        try
        {
            // Atomic inventory reservation for each product
            var holdItems = new List<HoldItem>();
            foreach (var item in request.Items)
            {
                var product = await _inventoryRepository.GetByProductIdAsync(item.ProductId)
                    ?? throw new KeyNotFoundException($"Product {item.ProductId} not found.");

                var decremented = await _inventoryRepository.DecrementAvailabilityAsync(item.ProductId, item.Quantity);
                if (!decremented)
                    throw new InvalidOperationException($"Insufficient stock for {product.ProductName}. Available: {product.AvailableQuantity}, requested: {item.Quantity}.");

                decrementedItems.Add((item.ProductId, item.Quantity));
                holdItems.Add(new HoldItem
                {
                    ProductId = item.ProductId,
                    ProductName = product.ProductName,
                    Quantity = item.Quantity
                });
            }

            var hold = new Hold
            {
                Id = Guid.NewGuid().ToString(),
                HoldId = holdId,
                Items = holdItems,
                Status = HoldStatus.Active,
                CreatedAt = now,
                ExpiresAt = expiresAt
            };

            await _holdRepository.CreateAsync(hold);
            await _cacheService.InvalidateInventoryAsync();

            // Publish events
            var eventItems = holdItems.Select(hi => new HoldItemDto
            {
                ProductId = hi.ProductId,
                ProductName = hi.ProductName,
                Quantity = hi.Quantity
            }).ToList();

            await _eventPublisher.PublishHoldCreatedAsync(holdId, eventItems, now, expiresAt);

            return new HoldDto
            {
                HoldId = hold.HoldId,
                Items = eventItems,
                Status = HoldStatus.Active,
                CreatedAt = now,
                ExpiresAt = expiresAt
            };
        }
        catch (Exception)
        {
            // Rollback: Restore inventory for any decremented items
            foreach (var (productId, quantity) in decrementedItems)
            {
                await _inventoryRepository.IncrementAvailabilityAsync(productId, quantity);
            }
            throw;
        }
    }

    public async Task<HoldDto?> GetHoldAsync(string holdId)
    {
        var hold = await _holdRepository.GetByHoldIdAsync(holdId);
        if (hold == null) return null;

        // Auto-expire if past expiration
        if (hold.IsActive && hold.IsExpired)
        {
            await ExpireHoldAsync(hold);
            hold.Status = HoldStatus.Expired;
        }

        return new HoldDto
        {
            HoldId = hold.HoldId,
            Items = hold.Items.Select(hi => new HoldItemDto
            {
                ProductId = hi.ProductId,
                ProductName = hi.ProductName,
                Quantity = hi.Quantity
            }).ToList(),
            Status = hold.Status == HoldStatus.Active ? HoldStatus.Active
                   : hold.Status == HoldStatus.Released ? HoldStatus.Released
                   : HoldStatus.Expired,
            CreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt,
            ReleasedAt = hold.ReleasedAt
        };
    }

    public async Task<ReleaseHoldResponse> ReleaseHoldAsync(string holdId)
    {
        var hold = await _holdRepository.GetByHoldIdAsync(holdId)
            ?? throw new KeyNotFoundException($"Hold {holdId} not found.");

        if (!hold.IsActive)
        {
            if (hold.Status == HoldStatus.Released || hold.Status == HoldStatus.Expired)
                throw new InvalidOperationException("Hold already released or expired.");
            throw new InvalidOperationException($"Hold is in {hold.Status} status.");
        }

        // Mark as released FIRST (optimistic lock), then restore inventory.
        // If status change fails, we throw before touching inventory.
        // If inventory restore fails after status change, the hold is correctly released
        // but inventory needs manual recovery — safer than double-incrementing.
        hold.Release();
        var released = await _holdRepository.ReleaseAsync(holdId);
        if (!released)
            throw new InvalidOperationException("Failed to release hold — concurrent modification.");

        foreach (var item in hold.Items)
        {
            await _inventoryRepository.IncrementAvailabilityAsync(item.ProductId, item.Quantity);
        }

        await _cacheService.InvalidateInventoryAsync();

        var eventItems = hold.Items.Select(hi => new HoldItemDto
        {
            ProductId = hi.ProductId,
            ProductName = hi.ProductName,
            Quantity = hi.Quantity
        }).ToList();

        await _eventPublisher.PublishHoldReleasedAsync(holdId, eventItems, hold.ReleasedAt!.Value);

        return new ReleaseHoldResponse
        {
            Message = "Hold released successfully",
            HoldId = holdId
        };
    }

    public async Task<List<HoldDto>> GetActiveHoldsAsync()
    {
        var holds = await _holdRepository.GetActiveHoldsAsync();
        return holds.Select(h => new HoldDto
        {
            HoldId = h.HoldId,
            Items = h.Items.Select(hi => new HoldItemDto
            {
                ProductId = hi.ProductId,
                ProductName = hi.ProductName,
                Quantity = hi.Quantity
            }).ToList(),
            Status = HoldStatus.Active,
            CreatedAt = h.CreatedAt,
            ExpiresAt = h.ExpiresAt
        }).ToList();
    }

    internal async Task ExpireHoldAsync(Hold hold)
    {
        foreach (var item in hold.Items)
        {
            await _inventoryRepository.IncrementAvailabilityAsync(item.ProductId, item.Quantity);
        }

        hold.Expire();
        await _holdRepository.MarkExpiredAsync(hold.HoldId);
        await _cacheService.InvalidateInventoryAsync();

        var eventItems = hold.Items.Select(hi => new HoldItemDto
        {
            ProductId = hi.ProductId,
            ProductName = hi.ProductName,
            Quantity = hi.Quantity
        }).ToList();

        await _eventPublisher.PublishHoldExpiredAsync(hold.HoldId, eventItems, DateTime.UtcNow);
    }
}
