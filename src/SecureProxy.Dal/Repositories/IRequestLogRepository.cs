using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public interface IRequestLogRepository
{
    void Add(RequestLog log);
}
