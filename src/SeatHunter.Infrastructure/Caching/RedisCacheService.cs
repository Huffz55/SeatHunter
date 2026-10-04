using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using SeatHunter.Application.Interfaces;

namespace SeatHunter.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cachedData = await _cache.GetStringAsync(key, cancellationToken);
        return cachedData == null ? default : JsonSerializer.Deserialize<T>(cachedData);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions { SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(5) };
        var serializedData = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, serializedData, options, cancellationToken);
    }
}
