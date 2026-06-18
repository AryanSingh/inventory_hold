using Contracts;
using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests;

public class HoldExpirationServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IServiceScope> _scope = new();
    private readonly Mock<IHoldRepository> _holdRepo = new();
    private readonly Mock<IInventoryRepository> _inventoryRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICacheService> _cacheService = new();

    public HoldExpirationServiceTests()
    {
        _scope.Setup(s => s.ServiceProvider).Returns(CreateServiceProvider());
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
    }

    private IServiceProvider CreateServiceProvider()
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IHoldRepository))).Returns(_holdRepo.Object);
        sp.Setup(s => s.GetService(typeof(IInventoryRepository))).Returns(_inventoryRepo.Object);
        sp.Setup(s => s.GetService(typeof(IEventPublisher))).Returns(_eventPublisher.Object);
        sp.Setup(s => s.GetService(typeof(ICacheService))).Returns(_cacheService.Object);
        return sp.Object;
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesExpiredHolds()
    {
        // Arrange
        var holdId = "expired-hold-1";
        var expiredHold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15)
        };

        _holdRepo.Setup(r => r.FindExpiredAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Hold> { expiredHold });
        _holdRepo.Setup(r => r.TryMarkExpiredAsync(holdId, HoldStatus.Active))
            .ReturnsAsync(true);

        var logger = new Mock<ILogger<HoldExpirationService>>();
        var service = new HoldExpirationService(_scopeFactory.Object, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _holdRepo.Verify(r => r.TryMarkExpiredAsync(holdId, HoldStatus.Active), Times.AtLeastOnce);
        _inventoryRepo.Verify(r => r.IncrementAvailabilityAsync("prod-1", 3), Times.AtLeastOnce);
        _cacheService.Verify(c => c.InvalidateInventoryAsync(), Times.AtLeastOnce);
        _eventPublisher.Verify(e => e.PublishHoldExpiredAsync(
            holdId, It.IsAny<List<HoldItemDto>>(), It.IsAny<DateTime>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsAlreadyClaimedHolds()
    {
        // Arrange
        var holdId = "claimed-hold-1";
        var expiredHold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem>
            {
                new() { ProductId = "prod-1", ProductName = "Widget", Quantity = 3 }
            },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15)
        };

        _holdRepo.Setup(r => r.FindExpiredAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Hold> { expiredHold });
        _holdRepo.Setup(r => r.TryMarkExpiredAsync(holdId, HoldStatus.Active))
            .ReturnsAsync(false);

        var logger = new Mock<ILogger<HoldExpirationService>>();
        var service = new HoldExpirationService(_scopeFactory.Object, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert
        _holdRepo.Verify(r => r.TryMarkExpiredAsync(holdId, HoldStatus.Active), Times.AtLeastOnce);
        _inventoryRepo.Verify(r => r.IncrementAvailabilityAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyExpiredList()
    {
        // Arrange
        _holdRepo.Setup(r => r.FindExpiredAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Hold>());

        var logger = new Mock<ILogger<HoldExpirationService>>();
        var service = new HoldExpirationService(_scopeFactory.Object, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Assert — no interactions with other services
        _holdRepo.Verify(r => r.TryMarkExpiredAsync(It.IsAny<string>(), It.IsAny<HoldStatus>()), Times.Never);
        _inventoryRepo.Verify(r => r.IncrementAvailabilityAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _eventPublisher.Verify(e => e.PublishHoldExpiredAsync(
            It.IsAny<string>(), It.IsAny<List<HoldItemDto>>(), It.IsAny<DateTime>()), Times.Never);
    }
}
