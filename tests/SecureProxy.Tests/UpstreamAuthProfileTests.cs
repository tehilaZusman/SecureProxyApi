using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SecureProxy.Bl.Security;
using SecureProxy.Dal.Domain;
using SecureProxy.Bl.Proxy;
using SecureProxy.Tests.Infrastructure;
using Xunit;

namespace SecureProxy.Tests;

public class UpstreamAuthProfileTests
{
    [Fact]
    public void Configured_bearer_profile_is_applied_from_secret_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UpstreamAuth:Profiles:CustomerApi"] = "test-secret-token"
            })
            .Build();
        var provider = new ConfigurationUpstreamAuthProfileProvider(configuration);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://upstream.test/");
        provider.Apply(request, new Destination { AuthProfileRef = "config:CustomerApi" });

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-secret-token"), request.Headers.Authorization);
    }

    [Fact]
    public void Missing_auth_profile_fails_closed()
    {
        var provider = new ConfigurationUpstreamAuthProfileProvider(new ConfigurationBuilder().Build());
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://upstream.test/");

        Assert.Throws<UpstreamAuthProfileException>(() =>
            provider.Apply(request, new Destination { AuthProfileRef = "config:Missing" }));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Missing_destination_auth_secret_is_rejected_without_forwarding()
    {
        using var harness = new ProxyTestHarness();
        harness.SeedOperation((destination, _) => destination.AuthProfileRef = "config:NotConfigured");

        var result = await harness.Executor.ExecuteAsync(
            TestRequests.Build("customer.get", new { urlId = 42 }),
            "auth-profile-test", TestRequests.NoHeaders, "application/json", CancellationToken.None);

        Assert.Equal(503, result.ProxyStatusCode);
        Assert.Equal(ReasonCodes.UpstreamAuthUnavailable, result.ReasonCode);
        Assert.False(result.ForwardingOccurred);
        Assert.False(harness.Handler.ReceivedAny);
    }

    [Fact]
    public void No_auth_profile_does_not_add_authorization_header()
    {
        var provider = new ConfigurationUpstreamAuthProfileProvider(new ConfigurationBuilder().Build());
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://upstream.test/");
        provider.Apply(request, new Destination());
        Assert.Null(request.Headers.Authorization);
    }
}
