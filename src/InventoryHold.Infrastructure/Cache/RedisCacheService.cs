using System.Text.Json;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace InventoryHold.Infrastructure.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheOptions;
    private const string CacheKey = "inventory:levels";

    public RedisCacheService(IDistributedCache cache, IConfiguration configuration)
    {
        _cache = cache;
        var ttlValue = configuration["Cache:TTLSeconds"];
        var ttlSeconds = int.TryParse(ttlValue, out var parsed) ? parsed : 30;
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
        };
    }

    public async Task<List<InventoryItem>?> GetInventoryAsync()
    {
        var cached = await _cache.GetStringAsync(CacheKey);
        if (string.IsNullOrEmpty(cached))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<InventoryItem>>(cached);
        }
        catch (JsonException)
        {
            // Corrupted cache data — invalidate and treat as miss
            await _cache.RemoveAsync(CacheKey);
            return null;
        }
    }

    public async Task SetInventoryAsync(List<InventoryItem> items)
    {
        var json = JsonSerializer.Serialize(items);
        await _cache.SetStringAsync(CacheKey, json, _cacheOptions);
    }

    public async Task InvalidateInventoryAsync()
    {
        await _cache.RemoveAsync(CacheKey);
    }
}
