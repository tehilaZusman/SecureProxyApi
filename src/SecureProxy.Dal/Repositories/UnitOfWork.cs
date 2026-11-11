using SecureProxy.Dal.Data;

namespace SecureProxy.Dal.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ProxyDbContext _context;

    public UnitOfWork(ProxyDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataAccessException("Failed to persist changes.", ex);
        }
    }
}
