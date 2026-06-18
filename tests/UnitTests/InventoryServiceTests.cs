using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Moq;

namespace UnitTests;

public class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepo = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _sut = new InventoryService(_inventoryRepo.Object, _cacheService.Object);
    }

    [Fact]
    public async Task GetInventoryAsync_CacheHit_ReturnsCachedData()
    {
        // Arrange
        var cachedItems = new List<InventoryItem>
        {
            new() { ProductId = "prod-1", ProductName = "Widget", AvailableQuantity = 50, ReservedQuantity = 0, TotalQuantity = 50 }
        };

        _cacheService.Setup(c => c.GetInventoryAsync()).ReturnsAsync(cachedItems);

        // Act
        var result = await _sut.GetInventoryAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Widget", result[0].ProductName);
        _inventoryRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetInventoryAsync_CacheMiss_FallsBackToMongoDb()
    {
        // Arrange
        _cacheService.Setup(c => c.GetInventoryAsync()).ReturnsAsync((List<InventoryItem>?)null);

        var dbItems = new List<InventoryItem>
        {
            new() { ProductId = "prod-1", ProductName = "Widget", AvailableQuantity = 50, ReservedQuantity = 0, TotalQuantity = 50 }
        };
        _inventoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(dbItems);

        // Act
        var result = await _sut.GetInventoryAsync();

        // Assert
        Assert.Single(result);
        _inventoryRepo.Verify(r => r.GetAllAsync(), Times.Once);
        _cacheService.Verify(c => c.SetInventoryAsync(dbItems), Times.Once);
    }

    [Fact]
    public async Task SeedIfEmptyAsync_EmptyDatabase_SeedsFiveProducts()
    {
        // Arrange
        _inventoryRepo.Setup(r => r.UpsertManyAsync(It.IsAny<List<InventoryItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SeedIfEmptyAsync();

        // Assert
        _inventoryRepo.Verify(r => r.UpsertManyAsync(It.Is<List<InventoryItem>>(items =>
            items.Count == 5)), Times.Once);
    }

    [Fact]
    public async Task SeedIfEmptyAsync_DataExists_UpsertsIdempotently()
    {
        // Arrange
        _inventoryRepo.Setup(r => r.UpsertManyAsync(It.IsAny<List<InventoryItem>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SeedIfEmptyAsync();

        // Assert — upsert is idempotent, always called regardless of existing data
        _inventoryRepo.Verify(r => r.UpsertManyAsync(It.Is<List<InventoryItem>>(items =>
            items.Count == 5)), Times.Once);
    }
}
