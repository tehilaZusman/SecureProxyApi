using System.Net;
using SecureProxy.Bl.Proxy;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class UpstreamBehaviorTests
{
    private const string Cid = "test-correlation";

    [Fact]
    public async Task Upstream_timeout_returns_gateway_timeout()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((dest, _) => dest.TimeoutMs = 100);

        h.Handler.Responder = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(504, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.UpstreamTimeout, result.ReasonCode);
    }

    [Fact]
    public async Task Upstream_error_status_is_preserved()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        h.Handler.Responder = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"nope\"}",
                    System.Text.Encoding.UTF8, "application/json")
            });

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        Assert.Equal(404, result.ProxyStatusCode);
        Assert.Equal(404, result.UpstreamStatusCode);
        Assert.Equal(ReasonCodes.Forwarded, result.ReasonCode);
    }

    [Fact]
    public async Task Upstream_connection_failure_returns_bad_gateway()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        h.Handler.Responder = (_, _) =>
            throw new HttpRequestException("connection refused");

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(502, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.UpstreamConnectionFailure, result.ReasonCode);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((dest, _) => dest.TimeoutMs = 30_000);

        using var callerCts = new CancellationTokenSource();

        h.Handler.Responder = async (_, ct) =>
        {
            callerCts.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", callerCts.Token);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(499, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.CallerCancelled, result.ReasonCode);
    }

    [Fact]
    public async Task Oversized_incoming_request_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.MaxRequestBytes = 16);

        var oversizedPayload = new string('A', 2000);

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }, encryptedPayload: oversizedPayload),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(413, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.RequestTooLarge, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Non_json_content_type_is_rejected_by_executor()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "text/plain", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(415, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.InvalidContentType, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Json_content_type_with_charset_is_accepted()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation();

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json; charset=utf-8", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
    }

    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.MaxResponseBytes = 10);

        h.Handler.Responder = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('a', 1000))
            });

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(502, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.ResponseTooLarge, result.ReasonCode);
    }
}
