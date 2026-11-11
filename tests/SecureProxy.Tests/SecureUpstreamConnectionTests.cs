using System.Net;
using SecureProxy.Api.Security;
using Xunit;

namespace SecureProxy.Tests;

public class SecureUpstreamConnectionTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.10.20")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.1.1")]
    [InlineData("192.168.1.10")]
    [InlineData("100.100.10.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.5")]
    [InlineData("203.0.113.7")]
    public void Private_or_local_ipv4_is_blocked_by_default(string value)
        => Assert.False(SecureUpstreamConnection.IsAllowed(IPAddress.Parse(value), allowPrivateNetworks: false));

    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.1.1")]
    [InlineData("192.168.1.10")]
    public void Private_ipv4_can_be_enabled_for_internal_upstreams(string value)
        => Assert.True(SecureUpstreamConnection.IsAllowed(IPAddress.Parse(value), allowPrivateNetworks: true));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    public void Public_ipv4_is_allowed(string value)
        => Assert.True(SecureUpstreamConnection.IsAllowed(IPAddress.Parse(value), allowPrivateNetworks: false));

    [Fact]
    public void Loopback_remains_blocked_even_when_private_networks_are_enabled()
        => Assert.False(SecureUpstreamConnection.IsAllowed(IPAddress.Loopback, allowPrivateNetworks: true));
}
