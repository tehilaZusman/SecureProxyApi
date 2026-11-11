using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;
using Xunit;

namespace SecureProxy.Tests;

public class ConcurrencyTests
{
    private static (SqliteConnection conn, DbContextOptions<ProxyDbContext> options) NewDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ProxyDbContext>().UseSqlite(conn).Options;
        using (var seed = new ProxyDbContext(options))
        {
            seed.Database.EnsureCreated();
            var dest = new Destination { Name = "d", BaseUrl = "https://u.test", IsActive = true };
            var op = new Operation
            {
                OperationKey = "customer.get", Destination = dest,
                HttpMethod = "GET", RelativePathTemplate = "/x", IsActive = true,
                RowVersion = BitConverter.GetBytes(1L)
            };
            seed.Add(dest);
            seed.Add(op);
            seed.SaveChanges();
        }
        return (conn, options);
    }

    [Fact]
    public void Stale_update_raises_concurrency_conflict()
    {
        var (conn, options) = NewDb();
        try
        {
            using var ctxA = new ProxyDbContext(options);
            using var ctxB = new ProxyDbContext(options);

            var opA = ctxA.Operations.Single(o => o.OperationKey == "customer.get");
            var opB = ctxB.Operations.Single(o => o.OperationKey == "customer.get");

            opA.IsActive = false;
            opA.RowVersion = BitConverter.GetBytes(2L);
            ctxA.SaveChanges();

            opB.IsActive = true;
            opB.RowVersion = BitConverter.GetBytes(3L);

            Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public void Non_conflicting_update_succeeds()
    {
        var (conn, options) = NewDb();
        try
        {
            using var ctx = new ProxyDbContext(options);
            var op = ctx.Operations.Single(o => o.OperationKey == "customer.get");
            op.IsActive = false;
            op.RowVersion = BitConverter.GetBytes(2L);
            var affected = ctx.SaveChanges();
            Assert.Equal(1, affected);
        }
        finally
        {
            conn.Dispose();
        }
    }
}
