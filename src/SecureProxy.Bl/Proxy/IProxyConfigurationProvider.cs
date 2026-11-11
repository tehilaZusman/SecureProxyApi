using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public interface IProxyConfigurationProvider
{
    Task<Operation?> GetOperationAsync(string operationKey, CancellationToken ct);

    void Invalidate(string? operationKey = null);
}
