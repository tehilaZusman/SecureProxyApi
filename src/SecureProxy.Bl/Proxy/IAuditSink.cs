using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Proxy;

public interface IAuditSink
{
    Task WriteAsync(RequestLog log, CancellationToken ct);
}
