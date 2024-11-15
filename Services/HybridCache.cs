using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace HidrydCache.Services;

public interface IHybridCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, DistributedCacheEntryOptions options);
}

internal sealed class HybridCache(
    IMemoryCache memoryCache,
    IDistributedCache distributedCache) : IHybridCache
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        DistributedCacheEntryOptions options)
    {
        if (memoryCache.TryGetValue(key, out T? value))
        {
            return value!;
        }

        // Si no está en memoria, busca en la caché distribuida
        var cachedData = await distributedCache.GetStringAsync(key);
        if (cachedData != null)
        {
            value = JsonSerializer.Deserialize<T>(cachedData);
            // Refrescar el valor en la memoria caché local para acceso rápido
            memoryCache.Set(key, value, options.AbsoluteExpirationRelativeToNow!.Value);
            return value!;
        }

        // Si no existe en ninguna caché, genera el valor con el factory
        value = await factory();

        // Serializar y almacenar en la caché distribuida
        var serializedData = JsonSerializer.Serialize(value);
        await distributedCache.SetStringAsync(key, serializedData, options);

        // También almacenar en la caché local
        memoryCache.Set(key, value, options.AbsoluteExpirationRelativeToNow!.Value);

        return value;
    }
}
