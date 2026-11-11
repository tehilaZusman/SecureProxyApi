using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class HttpMethodTests
{
    private const string Cid = "method-corr";

    [Fact]
    public async Task Configured_method_is_used_verbatim()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.HttpMethod = "GET");

        await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        var req = h.Handler.ReceivedRequests.Single();
        Assert.Equal(HttpMethod.Get, req.Method);
    }

    [Fact]
    public async Task Caller_cannot_change_method_via_parameters()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.HttpMethod = "GET");

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42, method = "DELETE" }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Post_operation_uses_post_method()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) =>
        {
            op.HttpMethod = "POST";
            op.RelativePathTemplate = "/posts";
            op.ParameterRules.Clear();
        });

        await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get"),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        var req = h.Handler.ReceivedRequests.Single();
        Assert.Equal(HttpMethod.Post, req.Method);
    }
}
