using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public sealed class RequestLogRepository : IRequestLogRepository
{
    private readonly ProxyDbContext _context;

    public RequestLogRepository(ProxyDbContext context)
    {
        _context = context;
    }

    public void Add(RequestLog log) => _context.RequestLogs.Add(log);
}
