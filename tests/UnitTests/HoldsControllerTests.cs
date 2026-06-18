using Contracts;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace UnitTests;

public class HoldsControllerTests
{
    private readonly Mock<IHoldRepository> _holdRepo = new();
    private readonly Mock<IInventoryRepository> _inventoryRepo = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly HoldService _holdService;
    private readonly HoldsController _controller;

    public HoldsControllerTests()
    {
        _holdService = new HoldService(
            _holdRepo.Object,
            _inventoryRepo.Object,
            _eventPublisher.Object,
            _cacheService.Object,
            defaultDurationMinutes: 15);
        var logger = new Mock<ILogger<HoldsController>>();
        _controller = new HoldsController(_holdService, logger.Object);
    }

    // --- CreateHold ---

    [Fact]
    public async Task CreateHold_ValidRequest_ReturnsCreatedAtAction()
    {
        var productId = "550e8400-e29b-41d4-a716-446655440001";
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = productId, Quantity = 5 }
            }
        };

        _inventoryRepo.Setup(r => r.GetByProductIdAsync(productId))
            .ReturnsAsync(new InventoryItem
            {
                ProductId = productId, ProductName = "Widget",
                AvailableQuantity = 50, ReservedQuantity = 0, TotalQuantity = 50
            });
        _inventoryRepo.Setup(r => r.DecrementAvailabilityAsync(productId, 5)).ReturnsAsync(true);
        _holdRepo.Setup(r => r.CreateAsync(It.IsAny<Hold>())).ReturnsAsync((Hold h) => h);

        var result = await _controller.CreateHold(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var hold = Assert.IsType<HoldDto>(createdResult.Value);
        Assert.Equal(HoldStatus.Active, hold.Status);
    }

    [Fact]
    public async Task CreateHold_NullItems_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest { Items = null! };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task CreateHold_EmptyItems_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest { Items = new List<CreateHoldItemRequest>() };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task CreateHold_InvalidProductId_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = "not-a-uuid", Quantity = 1 }
            }
        };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("Invalid ProductId format", problem.Detail);
    }

    [Fact]
    public async Task CreateHold_ZeroQuantity_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = "550e8400-e29b-41d4-a716-446655440001", Quantity = 0 }
            }
        };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("greater than 0", problem.Detail);
    }

    [Fact]
    public async Task CreateHold_ExcessiveQuantity_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = "550e8400-e29b-41d4-a716-446655440001", Quantity = 1001 }
            }
        };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("cannot exceed 1000", problem.Detail);
    }

    [Fact]
    public async Task CreateHold_TooManyItems_ReturnsBadRequest()
    {
        var items = Enumerable.Range(0, 51)
            .Select(i => new CreateHoldItemRequest
            {
                ProductId = $"550e8400-e29b-41d4-a716-44665544{i:D4}",
                Quantity = 1
            }).ToList();
        var request = new CreateHoldRequest { Items = items };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("Maximum 50 items", problem.Detail);
    }

    [Fact]
    public async Task CreateHold_InvalidDuration_ReturnsBadRequest()
    {
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = "550e8400-e29b-41d4-a716-446655440001", Quantity = 1 }
            },
            DurationMinutes = 0
        };
        var result = await _controller.CreateHold(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("between 1 and 1440", problem.Detail);
    }

    [Fact]
    public async Task CreateHold_ProductNotFound_ReturnsNotFound()
    {
        var productId = "550e8400-e29b-41d4-a716-446655440001";
        var request = new CreateHoldRequest
        {
            Items = new List<CreateHoldItemRequest>
            {
                new() { ProductId = productId, Quantity = 1 }
            }
        };
        _inventoryRepo.Setup(r => r.GetByProductIdAsync(productId))
            .ThrowsAsync(new KeyNotFoundException($"Product {productId} not found."));

        var result = await _controller.CreateHold(request);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task CreateHold_InsufficientStock_ReturnsConflict()
    {
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
                ProductId = productId, ProductName = "Widget",
                AvailableQuantity = 5, ReservedQuantity = 0, TotalQuantity = 5
            });
        _inventoryRepo.Setup(r => r.DecrementAvailabilityAsync(productId, 100)).ReturnsAsync(false);

        var result = await _controller.CreateHold(request);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    // --- GetActiveHolds ---

    [Fact]
    public async Task GetActiveHolds_ReturnsOk()
    {
        var holds = new List<HoldDto>
        {
            new() { HoldId = "h1", Status = HoldStatus.Active, Items = new(), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15) }
        };
        _holdRepo.Setup(r => r.GetActiveHoldsAsync()).ReturnsAsync(new List<Hold>
        {
            new() { HoldId = "h1", Status = HoldStatus.Active, Items = new(), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15) }
        });

        var result = await _controller.GetActiveHolds();
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // --- GetHold ---

    [Fact]
    public async Task GetHold_ExistingHold_ReturnsOk()
    {
        var holdId = "test-hold-123";
        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem> { new() { ProductId = "p1", ProductName = "W", Quantity = 1 } },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        var result = await _controller.GetHold(holdId);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetHold_NonExistent_ReturnsNotFound()
    {
        _holdRepo.Setup(r => r.GetByHoldIdAsync("nope")).ReturnsAsync((Hold?)null);
        var result = await _controller.GetHold("nope");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- ReleaseHold ---

    [Fact]
    public async Task ReleaseHold_ActiveHold_ReturnsOk()
    {
        var holdId = "test-hold-456";
        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem> { new() { ProductId = "p1", ProductName = "W", Quantity = 3 } },
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        _holdRepo.Setup(r => r.ReleaseAsync(holdId)).ReturnsAsync(true);

        var result = await _controller.ReleaseHold(holdId);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ReleaseHold_NonExistent_ReturnsNotFound()
    {
        _holdRepo.Setup(r => r.GetByHoldIdAsync("nope")).ReturnsAsync((Hold?)null);
        var result = await _controller.ReleaseHold("nope");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReleaseHold_AlreadyReleased_Returns410Gone()
    {
        var holdId = "test-hold-789";
        _holdRepo.Setup(r => r.GetByHoldIdAsync(holdId)).ReturnsAsync(new Hold
        {
            Id = Guid.NewGuid().ToString(),
            HoldId = holdId,
            Items = new List<HoldItem> { new() { ProductId = "p1", ProductName = "W", Quantity = 3 } },
            Status = HoldStatus.Released,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ReleasedAt = DateTime.UtcNow.AddMinutes(-2)
        });

        var result = await _controller.ReleaseHold(holdId);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(410, objectResult.StatusCode);
    }
}
