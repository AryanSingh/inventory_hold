using System.Text.Json;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private const string CacheKey = "inventory:levels";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
    };

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<List<InventoryItem>?> GetInventoryAsync()
    {
        var cached = await _cache.GetStringAsync(CacheKey);
        if (string.IsNullOrEmpty(cached))
            return null;

        return JsonSerializer.Deserialize<List<InventoryItem>>(cached);
    }

    public async Task SetInventoryAsync(List<InventoryItem> items)
    {
        var json = JsonSerializer.Serialize(items);
        await _cache.SetStringAsync(CacheKey, json, CacheOptions);
    }

    public async Task InvalidateInventoryAsync()
    {
        await _cache.RemoveAsync(CacheKey);
    }
}
