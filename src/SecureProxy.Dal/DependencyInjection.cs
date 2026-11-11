using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Repositories;

namespace SecureProxy.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddDalServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProxyDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SecureProxy;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddDbContext<ProxyDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOperationRepository, OperationRepository>();
        services.AddScoped<IRequestLogRepository, RequestLogRepository>();
        services.AddScoped<IConfigurationChangeLogRepository, ConfigurationChangeLogRepository>();

        return services;
    }
}
