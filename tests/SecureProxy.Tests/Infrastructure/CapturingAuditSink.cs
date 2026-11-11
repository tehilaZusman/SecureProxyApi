using System.Collections.Concurrent;
using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;

namespace SecureProxy.Tests.Infrastructure;

public sealed class CapturingAuditSink : IAuditSink
{
    public ConcurrentQueue<RequestLog> Logs { get; } = new();

    public Task WriteAsync(RequestLog log, CancellationToken ct)
    {
        Logs.Enqueue(log);
        return Task.CompletedTask;
    }
}
