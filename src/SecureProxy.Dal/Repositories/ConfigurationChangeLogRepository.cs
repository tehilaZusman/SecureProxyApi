using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public sealed class ConfigurationChangeLogRepository : IConfigurationChangeLogRepository
{
    private readonly ProxyDbContext _context;

    public ConfigurationChangeLogRepository(ProxyDbContext context)
    {
        _context = context;
    }

    public void Add(ConfigurationChangeLog entry) => _context.ConfigurationChangeLogs.Add(entry);
}
