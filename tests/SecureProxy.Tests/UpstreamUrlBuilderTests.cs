using SecureProxy.Bl.Proxy;
using SecureProxy.Dal.Domain;
using Xunit;

namespace SecureProxy.Tests;

public class UpstreamUrlBuilderTests
{
    private static (Destination dest, Operation op) Build(string baseUrl, string template)
    {
        var dest = new Destination { Name = "d", BaseUrl = baseUrl, IsActive = true };
        var op = new Operation
        {
            OperationKey = "k", Destination = dest,
            HttpMethod = "GET", RelativePathTemplate = template, IsActive = true
        };
        return (dest, op);
    }

    [Fact]
    public void Builds_url_under_the_configured_base_path()
    {
        var (dest, op) = Build("https://u.test/api", "/users/5");

        var result = UpstreamUrlBuilder.Build(dest, op, new ValidatedParameters());

        Assert.True(result.IsValid);
        Assert.Equal("https://u.test/api/users/5", result.Uri!.ToString());
    }

    [Fact]
    public void Base_path_equal_is_allowed()
    {
        var (dest, op) = Build("https://u.test/api", "/");

        var result = UpstreamUrlBuilder.Build(dest, op, new ValidatedParameters());

        Assert.True(result.IsValid);
        Assert.Equal("/api/", result.Uri!.AbsolutePath);
    }

    [Theory]
    [InlineData("/api", "/api", true)]
    [InlineData("/api", "/api/users", true)]
    [InlineData("/api", "/apix", false)]
    [InlineData("/api", "/api2/secret", false)]
    [InlineData("/api", "/apiusers", false)]
    public void Base_path_boundary_is_respected(string basePath, string candidate, bool expected)
    {
        var actual = InvokeIsUnderBasePath(candidate, basePath);
        Assert.Equal(expected, actual);
    }

    private static bool InvokeIsUnderBasePath(string absolutePath, string basePath)
    {
        var method = typeof(UpstreamUrlBuilder).GetMethod(
            "IsUnderBasePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object[] { absolutePath, basePath })!;
    }
}
