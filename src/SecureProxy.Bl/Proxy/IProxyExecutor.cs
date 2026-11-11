using SecureProxy.Bl.Dtos;

namespace SecureProxy.Bl.Proxy;

public interface IProxyExecutor
{
    Task<ProxyResult> ExecuteAsync(
        ProxyExecuteRequest request,
        string correlationId,
        IReadOnlyList<KeyValuePair<string, string>> incomingHeaders,
        string? contentType,
        CancellationToken callerToken,
        long incomingBodyBytes = -1);
}
