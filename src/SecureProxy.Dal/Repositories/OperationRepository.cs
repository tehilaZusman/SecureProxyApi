using Microsoft.EntityFrameworkCore;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Repositories;

public sealed class OperationRepository : IOperationRepository
{
    private readonly ProxyDbContext _context;

    public OperationRepository(ProxyDbContext context)
    {
        _context = context;
    }

    public Task<Operation?> GetByKeyAsync(string operationKey, CancellationToken ct = default)
        => ExecuteAsync(() => _context.Operations
            .AsNoTracking()
            .Include(o => o.Destination)
            .Include(o => o.ParameterRules)
            .Include(o => o.HeaderRules)
            .Include(o => o.EncryptionProfile)
            .SingleOrDefaultAsync(o => o.OperationKey == operationKey, ct));

    public Task<Operation?> GetTrackedByKeyAsync(string operationKey, CancellationToken ct = default)
        => ExecuteAsync(() => _context.Operations
            .SingleOrDefaultAsync(o => o.OperationKey == operationKey, ct));

    public void SetExpectedRowVersion(Operation tracked, byte[] expectedRowVersion)
    {
        _context.Entry(tracked).Property(o => o.RowVersion).OriginalValue = expectedRowVersion;
    }

    private static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> query)
    {
        try
        {
            return await query();
        }
        catch (Exception ex)
        {
            throw new DataAccessException("Database query failed.", ex);
        }
    }
}
