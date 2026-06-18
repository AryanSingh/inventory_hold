using Contracts;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Moq;

namespace UnitTests;

public class HoldServiceTests
{
    private readonly Mock<IHoldRepository> _holdRepo = new();
    private readonly Mock<IInventoryRepository> _inventoryRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly HoldService _sut;

    public HoldServiceTests()
    {
        _sut = new HoldService(
            _holdRepo.Object,
            _inventoryRepo.Object,
            _eventPublisher.Object,
            _cacheService.Object,
            defaultDurationMinutes: 15);
    }

    [Fact]
    public async Task CreateHoldAsync_ValidRequest_ReturnsActiveHold()
    {
        // Arrange
        var productId = "550e8400-e29b-41d4-a716-446655440001";
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = productId, Quantity = 2 }
            }
        };

        _inventoryRepo.Setup(r => r.GetByProductIdAsync(productId))
            .ReturnsAsync(new InventoryItem
            {
                ProductId = productId,
                ProductName = "Wireless Mouse",
                AvailableQuantity = 50,
                ReservedQuantity = 0,
                TotalQuantity = 50
            });

        _inventoryRepo.Setup(r => r.DecrementAvailabilityAsync(productId, 2))
            .ReturnsAsync(true);

        _holdRepo.Setup(r => r.CreateAsync(It.IsAny<Hold>()))
            .ReturnsAsync((Hold h) => h);

        // Act
        var result = await _sut.CreateHoldAsync(request);

        // Assert
        Assert.Equal(HoldStatus.Active, result.Status);
        Assert.Single(result.Items);
        Assert.Equal(productId, result.Items[0].ProductId);
        Assert.Equal(2, result.Items[0].Quantity);
        _inventoryRepo.Verify(r => r.DecrementAvailabilityAsync(productId, 2), Times.Once);
        _holdRepo.Verify(r => r.CreateAsync(It.IsAny<Hold>()), Times.Once);
        _cacheService.Verify(c => c.InvalidateInventoryAsync(), Times.Once);
        _eventPublisher.Verify(e => e.PublishHoldCreatedAsync(
            It.IsAny<string>(), It.IsAny<List<HoldItemDto>>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task CreateHoldAsync_EmptyItems_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateHoldRequest { Items = new List<CreateHoldItemRequest>() };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateHoldAsync(request));
        Assert.Contains("At least one item is required", ex.Message);
    }

    [Fact]
    public async Task CreateHoldAsync_InsufficientStock_ThrowsInvalidOperationException()
    {
        // Arrange
        var productId = "550e8400-e29b-41d4-a716-446655440001";
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = productId, Quantity = 100 }
            }
        };

        _inventoryRepo.Setup(r => r.GetByProductIdAsync(productId))
            .ReturnsAsync(new InventoryItem
            {
                ProductId = productId,
                ProductName = "Wireless Mouse",
                AvailableQuantity = 5,
                ReservedQuantity = 0,
                TotalQuantity = 5
            });

        _inventoryRepo.Setup(r => r.DecrementAvailabilityAsync(productId, 100))
            .ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateHoldAsync(request));
        Assert.Contains("Insufficient stock", ex.Message);
    }

    [Fact]
    public async Task GetHoldAsync_ExistingHold_ReturnsHoldDto()
    {
        // Arrange
        var holdId = "test-hold-123";
        var hold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(hold);

        // Act
        var result = await _sut.GetHoldAsync(holdId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(holdId, result!.HoldId);
        Assert.Equal(HoldStatus.Active, result.Status);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetHoldAsync_NonExistentHold_ReturnsNull()
    {
        // Arrange
        _holdRepo.Setup(r => r.GetByHoldIdAsync("nonexistent")).ReturnsAsync((Hold?)null);

        // Act
        var result = await _sut.GetHoldAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReleaseHoldAsync_ActiveHold_ReleasesAndRestoresInventory()
    {
        // Arrange
        var holdId = "test-hold-456";
        var hold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(hold);
        _holdRepo.Setup(r => r.ReleaseAsync(holdId)).ReturnsAsync(true);

        // Act
        var result = await _sut.ReleaseHoldAsync(holdId);

        // Assert
        Assert.Equal("Hold released successfully", result.Message);
        Assert.Equal(holdId, result.HoldId);
        _holdRepo.Verify(r => r.ReleaseAsync(holdId), Times.Once);
        _inventoryRepo.Verify(r => r.IncrementAvailabilityAsync("prod-1", 3), Times.Once);
        _cacheService.Verify(c => c.InvalidateInventoryAsync(), Times.Once);
        _eventPublisher.Verify(e => e.PublishHoldReleasedAsync(
            holdId, It.IsAny<List<HoldItemDto>>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseHoldAsync_ConcurrentModification_ThrowsInvalidOperationException()
    {
        // Arrange — ReleaseAsync returns false (concurrent modification)
        var holdId = "test-hold-concurrent";
        var hold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(hold);
        _holdRepo.Setup(r => r.ReleaseAsync(holdId)).ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReleaseHoldAsync(holdId));
        Assert.Contains("concurrent modification", ex.Message);
        // Inventory should NOT be restored since status change failed
        _inventoryRepo.Verify(r => r.IncrementAvailabilityAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseHoldAsync_NonExistentHold_ThrowsKeyNotFoundException()
    {
        // Arrange
        _holdRepo.Setup(r => r.GetByHoldIdAsync("nonexistent")).ReturnsAsync((Hold?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.ReleaseHoldAsync("nonexistent"));
    }

    [Fact]
    public async Task ReleaseHoldAsync_AlreadyReleased_ThrowsInvalidOperationException()
    {
        // Arrange
        var holdId = "test-hold-789";
        var hold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Released,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ReleasedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(hold);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReleaseHoldAsync(holdId));
        Assert.Contains("already released or expired", ex.Message);
    }
}
