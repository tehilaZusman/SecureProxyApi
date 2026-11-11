using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;

namespace SecureProxy.Tests.Infrastructure;

public sealed class ProxyWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public const string AdminApiKey = "test-admin-key";
    public const string ProxyApiKey = "test-proxy-key";

    public RecordingHttpHandler Handler { get; } = new();

    public ProxyWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Proxy-Api-Key", ProxyApiKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:ApiKey"] = AdminApiKey,
                ["Proxy:ApiKey"] = ProxyApiKey
            }));

        builder.ConfigureServices(services =>
        {
            RemoveAll(services, typeof(DbContextOptions<ProxyDbContext>));
            RemoveAll(services, typeof(ProxyDbContext));
            services.AddDbContext<ProxyDbContext>(o => o.UseSqlite(_connection));

            services.AddHttpClient(ProxyExecutor.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Handler);

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
            db.Database.EnsureCreated();
            if (!db.Operations.Any())
            {
                var dest = new Destination
                {
                    Name = "e2e", BaseUrl = "https://upstream.test", IsActive = true, TimeoutMs = 5000
                };
                var op = new Operation
                {
                    OperationKey = "customer.get", Destination = dest,
                    HttpMethod = "GET", RelativePathTemplate = "/users/{urlId}", IsActive = true,
                    RejectUnknownParameters = true,
                    ParameterRules =
                    {
                        new ParameterRule
                        {
                            Name = "urlId", Required = true,
                            DataType = ParameterDataType.Integer,
                            Location = ParameterLocation.Path, MinValue = 1, MaxValue = 100000
                        }
                    }
                };
                db.Add(dest);
                db.Add(op);
                db.SaveChanges();
            }
        });
    }

    private static void RemoveAll(IServiceCollection services, Type type)
    {
        var descriptors = services.Where(d => d.ServiceType == type).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
