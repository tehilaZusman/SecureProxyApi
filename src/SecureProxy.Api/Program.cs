using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using SecureProxy.Api.Middleware;
using SecureProxy.Api.Security;
using SecureProxy.Bl.Proxy;
using SecureProxy.Bl.Security;
using SecureProxy.Dal;
using SecureProxy.Dal.Data;

var builder = WebApplication.CreateBuilder(args);

var maxBodyBytes = builder.Configuration.GetValue<long>("Proxy:MaxRequestBytes", 1024 * 1024);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBodyBytes);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxBodyBytes);

var maxConnectionsPerServer =
    builder.Configuration.GetValue<int>("Proxy:MaxConnectionsPerServer", 64);

builder.Services.AddDalServices(builder.Configuration);

builder.Services.AddHttpClient(ProxyExecutor.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AllowAutoRedirect = false,
        UseProxy = false,
        MaxConnectionsPerServer = maxConnectionsPerServer,
        ConnectCallback = (context, ct) => SecureUpstreamConnection.ConnectAsync(
            context, ct, builder.Configuration.GetValue<bool>("Proxy:AllowPrivateNetworkDestinations", false))
    })
    .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);

builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);
builder.Services.AddSingleton<IKeySource, EnvironmentKeySource>();
builder.Services.AddSingleton<IPayloadCryptoService, AesGcmPayloadCryptoService>();
builder.Services.AddSingleton<IUpstreamAuthProfileProvider, ConfigurationUpstreamAuthProfileProvider>();
builder.Services.AddSingleton<ParameterValidator>();
builder.Services.AddScoped<IProxyConfigurationProvider, CachedConfigurationProvider>();
builder.Services.AddScoped<IAuditSink, DatabaseAuditSink>();
builder.Services.AddScoped<IProxyExecutor, ProxyExecutor>();

builder.Services.AddScoped<AdminApiKeyAuthorizationFilter>();
builder.Services.AddScoped<ProxyApiKeyAuthorizationFilter>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.Migrate();
        DbSeeder.Seed(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelationId();
app.UseMiddleware<RequestBodySizeTrackingMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }
