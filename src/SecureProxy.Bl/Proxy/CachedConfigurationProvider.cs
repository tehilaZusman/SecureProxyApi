using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using SecureProxy.Dal.Domain;
using SecureProxy.Dal.Repositories;

namespace SecureProxy.Bl.Proxy;

public sealed class CachedConfigurationProvider : IProxyConfigurationProvider
{
    private readonly IOperationRepository _operations;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    private const string KeyPrefix = "op:";

    public CachedConfigurationProvider(IOperationRepository operations, IMemoryCache cache, IConfiguration config)
    {
        _operations = operations;
        _cache = cache;
        var seconds = config.GetValue("Proxy:ConfigCacheSeconds", 30);
        _ttl = TimeSpan.FromSeconds(seconds <= 0 ? 30 : seconds);
    }

    public async Task<Operation?> GetOperationAsync(string operationKey, CancellationToken ct)
    {
        var cacheKey = KeyPrefix + operationKey;
        if (_cache.TryGetValue(cacheKey, out Operation? cached))
            return cached;

        var op = await _operations.GetByKeyAsync(operationKey, ct);

        _cache.Set(cacheKey, op, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl,
            Size = 1
        });

        return op;
    }

    public void Invalidate(string? operationKey = null)
    {
        if (operationKey is null)
        {
            if (_cache is MemoryCache mc)
                mc.Compact(1.0);
            return;
        }
        _cache.Remove(KeyPrefix + operationKey);
    }
}
