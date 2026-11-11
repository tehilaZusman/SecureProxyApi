using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public interface IOperationRepository
{
    Task<Operation?> GetByKeyAsync(string operationKey, CancellationToken ct = default);

    Task<Operation?> GetTrackedByKeyAsync(string operationKey, CancellationToken ct = default);

    void SetExpectedRowVersion(Operation tracked, byte[] expectedRowVersion);
}
