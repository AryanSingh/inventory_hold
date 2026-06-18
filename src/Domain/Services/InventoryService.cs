using Contracts;
using Domain.Interfaces;

namespace Domain.Services;

public class InventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICacheService _cacheService;

    public InventoryService(IInventoryRepository inventoryRepository, ICacheService cacheService)
    {
        _inventoryRepository = inventoryRepository;
        _cacheService = cacheService;
    }

    public async Task<List<InventoryItemDto>> GetInventoryAsync()
    {
        // Cache-aside: check cache first
        var cached = await _cacheService.GetInventoryAsync();
        if (cached != null)
        {
            return cached.Select(i => new InventoryItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                AvailableQuantity = i.AvailableQuantity,
                ReservedQuantity = i.ReservedQuantity,
                TotalQuantity = i.TotalQuantity,
                UpdatedAt = i.UpdatedAt
            }).ToList();
        }

        // Fallback to MongoDB
        var items = await _inventoryRepository.GetAllAsync();
        await _cacheService.SetInventoryAsync(items);

        return items.Select(i => new InventoryItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            AvailableQuantity = i.AvailableQuantity,
            ReservedQuantity = i.ReservedQuantity,
            TotalQuantity = i.TotalQuantity,
            UpdatedAt = i.UpdatedAt
        }).ToList();
    }

    public async Task SeedIfEmptyAsync()
    {
        // Race-safe: UpsertManyAsync is idempotent (atomic replace-or-create per ProductId).
        // Multiple instances can call this concurrently without duplicate key errors.
        var seedItems = GetSeedData();
        await _inventoryRepository.UpsertManyAsync(seedItems);
    }

    private static List<Domain.Entities.InventoryItem> GetSeedData()
    {
        return new List<Domain.Entities.InventoryItem>
        {
            new() { Id = "550e8400-e29b-41d4-a716-446655440001", ProductId = "550e8400-e29b-41d4-a716-446655440001", ProductName = "Wireless Mouse", AvailableQuantity = 50, ReservedQuantity = 0, TotalQuantity = 50, UpdatedAt = DateTime.UtcNow },
            new() { Id = "550e8400-e29b-41d4-a716-446655440002", ProductId = "550e8400-e29b-41d4-a716-446655440002", ProductName = "Mechanical Keyboard", AvailableQuantity = 30, ReservedQuantity = 0, TotalQuantity = 30, UpdatedAt = DateTime.UtcNow },
            new() { Id = "550e8400-e29b-41d4-a716-446655440003", ProductId = "550e8400-e29b-41d4-a716-446655440003", ProductName = "USB-C Hub", AvailableQuantity = 100, ReservedQuantity = 0, TotalQuantity = 100, UpdatedAt = DateTime.UtcNow },
            new() { Id = "550e8400-e29b-41d4-a716-446655440004", ProductId = "550e8400-e29b-41d4-a716-446655440004", ProductName = "Monitor Stand", AvailableQuantity = 25, ReservedQuantity = 0, TotalQuantity = 25, UpdatedAt = DateTime.UtcNow },
            new() { Id = "550e8400-e29b-41d4-a716-446655440005", ProductId = "550e8400-e29b-41d4-a716-446655440005", ProductName = "Webcam HD", AvailableQuantity = 75, ReservedQuantity = 0, TotalQuantity = 75, UpdatedAt = DateTime.UtcNow }
        };
    }
}
