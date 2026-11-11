using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SecureProxy.Dal.Domain;

namespace SecureProxy.Bl.Security;

public sealed class ConfigurationUpstreamAuthProfileProvider : IUpstreamAuthProfileProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationUpstreamAuthProfileProvider(IConfiguration configuration)
        => _configuration = configuration;

    public void Apply(HttpRequestMessage request, Destination destination)
    {
        if (string.IsNullOrWhiteSpace(destination.AuthProfileRef))
            return;

        var separator = destination.AuthProfileRef.IndexOf(':');
        if (separator <= 0 || separator == destination.AuthProfileRef.Length - 1)
            throw new UpstreamAuthProfileException();

        var scheme = destination.AuthProfileRef[..separator];
        var name = destination.AuthProfileRef[(separator + 1)..];
        var token = scheme switch
        {
            "env" => Environment.GetEnvironmentVariable(name),
            "config" => _configuration[$"UpstreamAuth:Profiles:{name}"],
            _ => null
        };

        if (string.IsNullOrWhiteSpace(token))
            throw new UpstreamAuthProfileException();

        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }
        catch (FormatException)
        {
            throw new UpstreamAuthProfileException();
        }
    }
}

public sealed class UpstreamAuthProfileException : Exception
{
    public UpstreamAuthProfileException() : base("Configured upstream authentication profile is unavailable or invalid.") { }
}
