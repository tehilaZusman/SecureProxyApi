using System.Net;
using System.Text;
using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class ProxyPolicyTests
{
    private const string Cid = "test-correlation";

    [Fact]
    public async Task Valid_operation_is_forwarded()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        Assert.Equal(200, result.ProxyStatusCode);
        Assert.Single(h.Handler.ReceivedRequests);
        var req = h.Handler.ReceivedRequests.First();
        Assert.Equal("https://upstream.test/users/42", req.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, req.Method);
    }

    [Fact]
    public async Task Unknown_operation_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("does.not.exist", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(404, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.OperationNotFound, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Disabled_operation_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.IsActive = false);

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(403, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.OperationDisabled, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Disabled_destination_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((dest, _) => dest.IsActive = false);

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(403, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.DestinationDisabled, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Caller_cannot_override_destination()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var req = TestRequests.Build("customer.get", new
        {
            urlId = 42,
            baseUrl = "https://evil.test",
            destination = "https://evil.test"
        });

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.UnknownParameter, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Caller_override_ignored_even_when_unknown_params_allowed()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.RejectUnknownParameters = false);

        var req = TestRequests.Build("customer.get", new
        {
            urlId = 42,
            baseUrl = "https://evil.test"
        });

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        var forwarded = h.Handler.ReceivedRequests.Single();
        Assert.Equal("upstream.test", forwarded.RequestUri!.Host);
        Assert.StartsWith("https://upstream.test/users/42", forwarded.RequestUri!.ToString());
    }

    [Fact]
    public async Task Missing_required_parameter_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.MissingParameter, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Unknown_parameter_rejected_under_strict_policy()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42, extra = "x" }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.UnknownParameter, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Unknown_parameter_allowed_when_policy_permits()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.RejectUnknownParameters = false);

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42, extra = "x" }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        Assert.Single(h.Handler.ReceivedRequests);
    }

    [Fact]
    public async Task Invalid_parameter_type_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = "not-a-number" }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidParameters, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Path_traversal_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) =>
        {
            op.RelativePathTemplate = "/files/{name}";
            op.ParameterRules.Clear();
            op.ParameterRules.Add(new ParameterRule
            {
                Name = "name", Required = true,
                DataType = ParameterDataType.String, Location = ParameterLocation.Path,
                MaxLength = 100
            });
        });

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { name = "../../etc/passwd" }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.PathTraversal, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Disallowed_headers_are_not_forwarded()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) =>
            op.HeaderRules.Add(new HeaderRule { HeaderName = "Accept-Language" }));

        var headers = new List<KeyValuePair<string, string>>
        {
            new("Accept-Language", "en-US"),
            new("Authorization", "Bearer secret-token"),
            new("Cookie", "session=abc"),
            new("X-Proxy-Api-Key", "consumer-secret"),
            new("X-Admin-Api-Key", "admin-secret"),
            new("X-Custom", "should-not-pass")
        };

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, headers, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        var req = h.Handler.ReceivedRequests.Single();

        Assert.True(req.Headers.Contains("Accept-Language"));
        Assert.False(req.Headers.Contains("Authorization"));
        Assert.False(req.Headers.Contains("Cookie"));
        Assert.False(req.Headers.Contains("X-Proxy-Api-Key"));
        Assert.False(req.Headers.Contains("X-Admin-Api-Key"));
        Assert.False(req.Headers.Contains("X-Custom"));
    }
}
