using Contracts;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Background;

public class HoldExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldExpirationService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public HoldExpirationService(IServiceScopeFactory scopeFactory, ILogger<HoldExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredHoldsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired holds");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredHoldsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var holdRepository = scope.ServiceProvider.GetRequiredService<IHoldRepository>();
        var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var expiredHolds = await holdRepository.FindExpiredAsync(DateTime.UtcNow);

        foreach (var hold in expiredHolds)
        {
            try
            {
                _logger.LogInformation("Attempting to expire hold {HoldId}", hold.HoldId);

                // Optimistic locking: Try to mark as expired first
                var marked = await holdRepository.TryMarkExpiredAsync(hold.HoldId, hold.Status);
                if (!marked)
                {
                    _logger.LogInformation("Hold {HoldId} already processed, skipping", hold.HoldId);
                    continue;
                }

                // Restore inventory
                foreach (var item in hold.Items)
                {
                    await inventoryRepository.IncrementAvailabilityAsync(item.ProductId, item.Quantity);
                }

                // Invalidate cache
                await cacheService.InvalidateInventoryAsync();

                // Publish event
                var eventItems = hold.Items.Select(hi => new HoldItemDto
                {
                    ProductId = hi.ProductId,
                    ProductName = hi.ProductName,
                    Quantity = hi.Quantity
                }).ToList();

                await eventPublisher.PublishHoldExpiredAsync(hold.HoldId, eventItems, DateTime.UtcNow);

                _logger.LogInformation("Successfully expired hold {HoldId}", hold.HoldId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring hold {HoldId}", hold.HoldId);
            }
        }
    }
}
