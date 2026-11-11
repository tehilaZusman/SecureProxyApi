using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureProxy.Bl.Proxy;
using SecureProxy.Dal.Data;
using SecureProxy.Dal.Domain;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class EndToEndApiTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public EndToEndApiTests(ProxyWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Valid_request_is_forwarded_and_returns_200()
    {
        _factory.Handler.Responder = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":42}", Encoding.UTF8, "application/json")
            });

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/proxy/execute", new
        {
            clientId = "customer.get",
            parameters = new { urlId = 42 }
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(resp.Headers.Contains("X-Correlation-Id"));
    }


    [Fact]
    public async Task Missing_proxy_api_key_is_rejected_before_forwarding()
    {
        _factory.Handler.ReceivedRequests.Clear();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Proxy-Api-Key");
        var response = await client.PostAsJsonAsync("/api/proxy/execute", new
        {
            clientId = "customer.get",
            parameters = new { urlId = 42 }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(_factory.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Invalid_proxy_api_key_is_rejected_before_forwarding()
    {
        _factory.Handler.ReceivedRequests.Clear();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Proxy-Api-Key");
        client.DefaultRequestHeaders.Add("X-Proxy-Api-Key", "wrong-key");
        var response = await client.PostAsJsonAsync("/api/proxy/execute", new
        {
            clientId = "customer.get",
            parameters = new { urlId = 42 }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(_factory.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Per_operation_limit_uses_actual_incoming_http_body_bytes()
    {
        long previousLimit;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
            var operation = await db.Operations.SingleAsync(x => x.OperationKey == "customer.get");
            previousLimit = operation.MaxRequestBytes;
            operation.MaxRequestBytes = 30;
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IProxyConfigurationProvider>().Invalidate("customer.get");
        }

        try
        {
            _factory.Handler.ReceivedRequests.Clear();
            var client = _factory.CreateClient();
            var body = "{\"clientId\":\"customer.get\",\"parameters\":{\"urlId\":42},\"ignoredPadding\":\"this-is-not-modeled-and-must-count\"}";
            var response = await client.PostAsync("/api/proxy/execute", new StringContent(body, Encoding.UTF8, "application/json"));

            Assert.Equal((HttpStatusCode)413, response.StatusCode);
            Assert.False(_factory.Handler.ReceivedAny);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
            var operation = await db.Operations.SingleAsync(x => x.OperationKey == "customer.get");
            operation.MaxRequestBytes = previousLimit;
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IProxyConfigurationProvider>().Invalidate("customer.get");
        }
    }

    [Fact]
    public async Task Upstream_404_status_is_preserved_through_the_pipeline()
    {
        _factory.Handler.Responder = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"nope\"}", Encoding.UTF8, "application/json")
            });

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/proxy/execute", new
        {
            clientId = "customer.get",
            parameters = new { urlId = 42 }
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Unknown_operation_returns_404_and_does_not_forward()
    {
        _factory.Handler.ReceivedRequests.Clear();

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/proxy/execute", new
        {
            clientId = "no.such.op",
            parameters = new { urlId = 42 }
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Non_json_content_type_is_rejected()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("clientId=customer.get", Encoding.UTF8, "text/plain");
        var resp = await client.PostAsync("/api/proxy/execute", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_from_controller_to_executor()
    {
        var correlationId = "cancel-e2e-" + Guid.NewGuid().ToString("N");
        var upstreamEntered = new TaskCompletionSource();

        _factory.Handler.Responder = async (_, ct) =>
        {
            upstreamEntered.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var client = _factory.CreateClient();
        using var callerCts = new CancellationTokenSource();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/proxy/execute")
        {
            Content = JsonContent.Create(new { clientId = "customer.get", parameters = new { urlId = 42 } })
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var sendTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, callerCts.Token);

        await upstreamEntered.Task;
        callerCts.Cancel();

        try
        {
            await sendTask;
        }
        catch (OperationCanceledException)
        {
        }

        var log = await WaitForAuditRecordAsync(correlationId);
        Assert.NotNull(log);
        Assert.False(log!.ForwardingOccurred);
        Assert.Equal(499, log.ProxyStatusCode);
        Assert.Equal(ReasonCodes.CallerCancelled, log.ReasonCode);
    }

    private async Task<RequestLog?> WaitForAuditRecordAsync(string correlationId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
            var log = await db.RequestLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.CorrelationId == correlationId);
            if (log is not null)
                return log;

            await Task.Delay(100);
        }

        return null;
    }

    [Fact]
    public async Task Configuration_change_history_records_admin_service_actor()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", ProxyWebApplicationFactory.AdminApiKey);
        var response = await client.PatchAsJsonAsync("/api/admin/operations/customer.get/enabled",
            new { isActive = true, rowVersion = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
        var change = await db.ConfigurationChangeLogs.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal("admin-api-key", change.ChangedBy);
    }
}

public class AdminEndToEndApiTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public AdminEndToEndApiTests(ProxyWebApplicationFactory factory) => _factory = factory;

    private sealed record SetEnabledBody(bool isActive, string? rowVersion);

    private HttpClient AuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", ProxyWebApplicationFactory.AdminApiKey);
        return client;
    }

    [Fact]
    public async Task Disable_then_enable_round_trips_through_repository_and_uow()
    {
        var client = AuthorizedClient();

        var disable = await client.PatchAsync("/api/admin/operations/customer.get/enabled",
            JsonContent.Create(new SetEnabledBody(false, null)));
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var enable = await client.PatchAsync("/api/admin/operations/customer.get/enabled",
            JsonContent.Create(new SetEnabledBody(true, null)));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
    }

    [Fact]
    public async Task Unknown_operation_returns_404()
    {
        var client = AuthorizedClient();
        var resp = await client.PatchAsync("/api/admin/operations/no.such.op/enabled",
            JsonContent.Create(new SetEnabledBody(false, null)));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Stale_rowversion_returns_409_conflict()
    {
        var client = AuthorizedClient();

        var staleRowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
        var resp = await client.PatchAsync("/api/admin/operations/customer.get/enabled",
            JsonContent.Create(new SetEnabledBody(false, staleRowVersion)));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Missing_admin_key_is_rejected_and_does_not_mutate()
    {
        var client = _factory.CreateClient();
        var resp = await client.PatchAsync("/api/admin/operations/customer.get/enabled",
            JsonContent.Create(new SetEnabledBody(false, null)));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.True(await IsOperationActive("customer.get"),
            "operation must remain active: an unauthorized call must not change configuration");
    }

    [Fact]
    public async Task Wrong_admin_key_is_rejected_and_does_not_mutate()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", "not-the-real-key");

        var resp = await client.PatchAsync("/api/admin/operations/customer.get/enabled",
            JsonContent.Create(new SetEnabledBody(false, null)));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.True(await IsOperationActive("customer.get"),
            "operation must remain active: a forbidden call must not change configuration");
    }

    private async Task<bool> IsOperationActive(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProxyDbContext>();
        var op = await db.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.OperationKey == key);
        return op?.IsActive ?? false;
    }
}
