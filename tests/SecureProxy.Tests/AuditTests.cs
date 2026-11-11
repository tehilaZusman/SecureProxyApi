using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class AuditTests
{
    private const string Cid = "audit-corr";

    [Fact]
    public async Task Audit_record_created_for_forwarded_request()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        var log = Assert.Single(h.Audit.Logs);
        Assert.Equal(Cid, log.CorrelationId);
        Assert.Equal("customer.get", log.OperationKey);
        Assert.True(log.ForwardingOccurred);
        Assert.Equal(ExecutionOutcome.Forwarded, log.Outcome);
        Assert.Equal(200, log.UpstreamStatusCode);
        Assert.True(log.DurationMs >= 0);
    }

    [Fact]
    public async Task Audit_record_created_for_rejected_request()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.IsActive = false);

        await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        var log = Assert.Single(h.Audit.Logs);
        Assert.False(log.ForwardingOccurred);
        Assert.Equal(ExecutionOutcome.Rejected, log.Outcome);
        Assert.Equal(ReasonCodes.OperationDisabled, log.ReasonCode);
    }

    [Fact]
    public async Task Audit_record_contains_no_sensitive_fields()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.HeaderRules.Add(new HeaderRule { HeaderName = "Accept-Language" }));

        var headers = new List<KeyValuePair<string, string>>
        {
            new("Authorization", "Bearer super-secret"),
            new("Accept-Language", "en")
        };

        await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, headers, "application/json", CancellationToken.None);

        var log = Assert.Single(h.Audit.Logs);
        Assert.DoesNotContain("super-secret", log.ReasonCode ?? "");
        Assert.DoesNotContain("super-secret", log.OperationKey ?? "");
    }
}
