using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;
using SecureProxy.Dal.Repositories;
using SecureProxy.Bl.Proxy;
using SecureProxy.Bl.Security;

namespace SecureProxy.Tests.Infrastructure;

public sealed class ProxyTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProxyDbContext Db { get; }
    public RecordingHttpHandler Handler { get; }
    public CapturingAuditSink Audit { get; }
    public ProxyExecutor Executor { get; }
    public AesGcmPayloadCryptoService Crypto { get; }
    public string EncryptionKeyRef => "config:Test";

    public ProxyTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ProxyDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new ProxyDbContext(options);
        Db.Database.EnsureCreated();

        Handler = new RecordingHttpHandler();
        Audit = new CapturingAuditSink();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Proxy:ConfigCacheSeconds"] = "30",
                ["Encryption:Keys:Test"] = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
            })
            .Build();

        var keySource = new EnvironmentKeySource(config);
        Crypto = new AesGcmPayloadCryptoService(keySource);

        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        var operationRepository = new OperationRepository(Db);
        var configProvider = new CachedConfigurationProvider(operationRepository, cache, config);

        Executor = new ProxyExecutor(
            configProvider,
            new TestHttpClientFactory(Handler),
            new ParameterValidator(),
            Crypto,
            new ConfigurationUpstreamAuthProfileProvider(config),
            Audit,
            NullLogger<ProxyExecutor>.Instance);
    }

    public Operation SeedOperation(Action<Destination, Operation>? customize = null)
    {
        var destination = new Destination
        {
            Name = "test-dest-" + Guid.NewGuid().ToString("N")[..8],
            BaseUrl = "https://upstream.test",
            IsActive = true,
            TimeoutMs = 5_000
        };

        var operation = new Operation
        {
            OperationKey = "customer.get",
            Destination = destination,
            HttpMethod = "GET",
            RelativePathTemplate = "/users/{urlId}",
            IsActive = true,
            MaxRequestBytes = 64 * 1024,
            MaxResponseBytes = 1024 * 1024,
            RejectUnknownParameters = true,
            ParameterRules =
            {
                new ParameterRule
                {
                    Name = "urlId", Required = true,
                    DataType = ParameterDataType.Integer, Location = ParameterLocation.Path,
                    MinValue = 1, MaxValue = 100000
                }
            }
        };

        customize?.Invoke(destination, operation);

        Db.Destinations.Add(destination);
        Db.Operations.Add(operation);
        Db.SaveChanges();
        return operation;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
