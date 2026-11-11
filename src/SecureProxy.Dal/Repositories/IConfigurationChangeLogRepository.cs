using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public interface IConfigurationChangeLogRepository
{
    void Add(ConfigurationChangeLog entry);
}
