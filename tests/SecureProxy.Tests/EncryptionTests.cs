using System.Text;
using Microsoft.Extensions.Configuration;
using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;
using SecureProxy.Bl.Security;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class EncryptionTests
{
    private const string Cid = "test-correlation";

    private static Operation SeedEncryptedOp(ProxyTestHarness h)
    {
        return h.SeedOperation((_, op) =>
        {
            op.HttpMethod = "POST";
            op.RelativePathTemplate = "/posts";
            op.EncryptionRequired = true;
            op.EncryptionProfile = new EncryptionProfile
            {
                Name = "p-" + Guid.NewGuid().ToString("N")[..8],
                Algorithm = "AES-GCM",
                KeyRef = h.EncryptionKeyRef
            };
            op.ParameterRules.Clear();
            op.ParameterRules.Add(new ParameterRule
            {
                Name = "urlId", Required = false,
                DataType = ParameterDataType.Integer, Location = ParameterLocation.Query,
                MinValue = 1, MaxValue = 100000
            });
            op.ParameterRules.Add(new ParameterRule
            {
                Name = "title", Required = true,
                DataType = ParameterDataType.String, Location = ParameterLocation.Body,
                MaxLength = 200
            });
        });
    }

    [Fact]
    public async Task Valid_encrypted_payload_is_decrypted_and_forwarded()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var plaintext = Encoding.UTF8.GetBytes("{\"title\":\"hello\"}");
        var payload = h.Crypto.Encrypt(h.EncryptionKeyRef, plaintext);

        var req = TestRequests.Build("customer.get", parameters: null, encryptedPayload: payload);

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        Assert.Single(h.Handler.ReceivedRequests);
    }

    [Fact]
    public async Task Tampered_auth_tag_is_rejected()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var plaintext = Encoding.UTF8.GetBytes("{\"title\":\"hello\"}");
        var payload = h.Crypto.Encrypt(h.EncryptionKeyRef, plaintext);

        var bytes = Convert.FromBase64String(payload);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        var req = TestRequests.Build("customer.get", parameters: null, encryptedPayload: tampered);

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidEncryptedPayload, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Malformed_payload_is_never_forwarded()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var req = TestRequests.Build("customer.get", parameters: null,
            encryptedPayload: "!!!not-base64!!!");

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidEncryptedPayload, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Missing_payload_when_required_is_rejected()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var req = TestRequests.Build("customer.get", new { title = "hello" });

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.EncryptionRequired, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Payload_encrypted_with_wrong_key_is_rejected()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var otherConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Keys:Test"] = "ZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmZmY="
            }).Build();
        var otherCrypto = new AesGcmPayloadCryptoService(new EnvironmentKeySource(otherConfig));
        var payload = otherCrypto.Encrypt(h.EncryptionKeyRef, Encoding.UTF8.GetBytes("{\"title\":\"x\"}"));

        var req = TestRequests.Build("customer.get", parameters: null, encryptedPayload: payload);

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidEncryptedPayload, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }
}

public class DecryptedPayloadShapeTests
{
    private const string Cid = "test-correlation";

    private static void SeedEncryptedOp(ProxyTestHarness h)
    {
        h.SeedOperation((_, op) =>
        {
            op.HttpMethod = "POST";
            op.RelativePathTemplate = "/posts";
            op.EncryptionRequired = true;
            op.EncryptionProfile = new EncryptionProfile
            {
                Name = "p-" + Guid.NewGuid().ToString("N")[..8],
                Algorithm = "AES-GCM",
                KeyRef = h.EncryptionKeyRef
            };
            op.ParameterRules.Clear();
            op.RejectUnknownParameters = false;
        });
    }

    [Fact]
    public async Task Decrypted_scalar_payload_is_rejected()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var payload = h.Crypto.Encrypt(h.EncryptionKeyRef, Encoding.UTF8.GetBytes("42"));
        var req = TestRequests.Build("customer.get", parameters: null, encryptedPayload: payload);

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidEncryptedPayload, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Fact]
    public async Task Decrypted_array_payload_is_rejected()
    {
        using var h = new ProxyTestHarness();
        SeedEncryptedOp(h);

        var payload = h.Crypto.Encrypt(h.EncryptionKeyRef, Encoding.UTF8.GetBytes("[1,2,3]"));
        var req = TestRequests.Build("customer.get", parameters: null, encryptedPayload: payload);

        var result = await h.Executor.ExecuteAsync(
            req, Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InvalidEncryptedPayload, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }
}

public class HttpMethodAllowlistTests
{
    private const string Cid = "test-correlation";

    [Fact]
    public async Task Unknown_configured_method_is_rejected()
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) => op.HttpMethod = "TRACE");

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.False(result.ForwardingOccurred);
        Assert.Equal(ReasonCodes.InternalError, result.ReasonCode);
        Assert.False(h.Handler.ReceivedAny);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    public async Task Allowed_methods_are_forwarded(string httpMethod)
    {
        using var h = new ProxyTestHarness();
        h.SeedOperation((_, op) =>
        {
            op.HttpMethod = httpMethod;
            op.RelativePathTemplate = "/users/{urlId}";
        });

        var result = await h.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            Cid, TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.True(result.ForwardingOccurred);
        var req = h.Handler.ReceivedRequests.Single();
        Assert.Equal(httpMethod, req.Method.Method);
    }
}
